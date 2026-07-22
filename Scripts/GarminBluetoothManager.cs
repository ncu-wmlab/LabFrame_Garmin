using System;
using System.Collections;
using System.Collections.Generic;
using LabFrame2023;
using UnityEngine;

/// <summary>
/// 連到 Android 端的 Garmin Streaming App 接收生理資料。
/// 預設走 TCP；藍牙 SPP 為可選傳輸，由 Inspector 明確指定，不會自動切換。
/// </summary>
public class GarminBluetoothManager : LabSingleton<GarminBluetoothManager>, IManager
{
    [Header("傳輸方式（不自動切換，網路不通就明確失敗）")]
    [SerializeField] private GarminTransport transport = GarminTransport.Tcp;

    [Header("TCP")]
    [SerializeField] private string manualHost = "";
    [SerializeField] private int manualPort = 6001;

    [Header("Bluetooth（僅 Android/Quest）")]
    [SerializeField] private string bluetoothDeviceName = "";

    [Header("連線行為")]
    [SerializeField] private float connectTimeoutSec = 10f;
    [SerializeField] private float reconnectIntervalSec = 2f;
    [SerializeField] private float noDataWarningSec = 5f;
    [Tooltip("相鄰兩則指令的最小間隔。Android 端讀的是原始 chunk 不是逐行，" +
             "兩則指令若黏在一起會被當成一則解析。")]
    [SerializeField] private float commandGapSec = 0.15f;

    private const string LogPrefix = "[GarminBluetoothManager]";

    private readonly GarminSessionState _session = new GarminSessionState();
    private readonly Queue<string> _outgoing = new Queue<string>();

    private IGarminLink _link;
    private Coroutine _lifecycle;
    private bool _userDisconnected;
    private bool _protocolMismatchReported;

    /// <summary>目前的資料。</summary>
    public LabGarminData labGarminData = new LabGarminData();

    /// <summary>目前是否已連線。</summary>
    public bool connectionStatus => _link != null && _link.State == GarminLinkState.Connected;

    /// <summary>是否偵測到協定不符（Android 端多半停在 JSONL）。資料全數不可用。</summary>
    public bool ProtocolMismatchDetected => _protocolMismatchReported;

    /* -------------------------------------------------------------------------- */

    public void ManagerInit()
    {
        Debug.Log($"{LogPrefix} ManagerInit（transport={transport}）");
    }

    public IEnumerator ManagerDispose()
    {
        DisconnectDevice();
        yield break;
    }

    /// <summary>
    /// 場景切換或 Editor 停止 Play 時，ManagerDispose 不保證會被呼叫。
    /// 這裡確保 socket 一定會被關掉——Android 端的 CommandServer 在有 client 掛著時
    /// 不會監聽新連線，遺留的 socket 會讓後續所有連線嘗試都被拒絕。
    /// </summary>
    protected override void DoOnDestroy()
    {
        _link?.Dispose();
        _link = null;
    }

    private void OnValidate()
    {
#if !UNITY_ANDROID
        if (transport == GarminTransport.Bluetooth)
        {
            Debug.LogWarning($"{LogPrefix} 目前建置目標不是 Android，藍牙傳輸無法使用，請改用 TCP。");
        }
#endif
    }

    /* -------------------------------------------------------------------------- */

    /// <summary>
    /// 以字串下達動作，方便綁在 UI Button 上。
    /// connect / start / start,tag / stop / disconnect，其餘字串視為 tag。
    /// </summary>
    public void ConnectionManager(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            Debug.LogWarning($"{LogPrefix} 收到空白指令，已忽略");
            return;
        }

        string command = message.Trim();
        GarminCommandKind kind = _session.Interpret(command);

        switch (kind)
        {
            case GarminCommandKind.Connect:
                // Android 在被動接聽下收到 connect 不會有任何動作（規格 §4），
                // 送出去只會增加指令黏包的風險，所以這裡只在本地啟動連線。
                StartLink();
                return;

            case GarminCommandKind.Disconnect:
                DisconnectDevice();
                return;

            case GarminCommandKind.Stop:
                // 尚未連線時不該為了送 stop 而建立連線
                if (_lifecycle != null)
                {
                    EnqueueCommand(command);
                }
                return;

            default:
                // start 與 tag 都要讓對面知道，必要時順帶把連線帶起來
                StartLink();
                EnqueueCommand(command);
                return;
        }
    }

    /// <summary>設定 TCP 連線目標。供實驗腳本在執行期指定，取代 Inspector 設定。</summary>
    public void SetTcpTarget(string host, int port)
    {
        manualHost = host;
        manualPort = port;
        Debug.Log($"{LogPrefix} TCP 目標設為 {host}:{port}");
    }

    /// <summary>
    /// 送出任意字串給 Android 端。與 ConnectionManager 走同一條路徑，
    /// 以免經由這裡送出的 tag 沒有被記錄、重連後補送時遺失。
    /// </summary>
    public void SendData(string data)
    {
        ConnectionManager(data);
    }

    /// <summary>主動斷線，並停止自動重連。</summary>
    public void DisconnectDevice()
    {
        Debug.Log($"{LogPrefix} 使用者主動斷線");
        _userDisconnected = true;

        if (_link != null && _link.State == GarminLinkState.Connected)
        {
            _link.Send("disconnect");
        }

        if (_lifecycle != null)
        {
            StopCoroutine(_lifecycle);
            _lifecycle = null;
        }

        _link?.Dispose();
        _link = null;
        _outgoing.Clear();
    }

    /* -------------------------------------------------------------------------- */

    private void StartLink()
    {
        _userDisconnected = false;
        if (_lifecycle == null)
        {
            _lifecycle = StartCoroutine(LifecycleLoop());
        }
    }

    private void EnqueueCommand(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return;
        }
        _outgoing.Enqueue(command.Trim());
    }

    /// <summary>
    /// 佇列裡是否已經有會開啟串流的指令。連線成功後補送 start 之前要先問這個：
    /// 若使用者在連線建立前就按了 start，該指令已排在佇列中，補送會變成送兩次。
    /// Android 端的 startSession 有 isRecording 防護所以不會出錯，但多送一則指令
    /// 徒增指令黏包的風險，log 也會出現兩次「已送出」造成誤判。
    /// </summary>
    private bool HasPendingStartCommand()
    {
        foreach (string queued in _outgoing)
        {
            if (queued.StartsWith("start", StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    private IGarminLink CreateLink()
    {
        if (transport == GarminTransport.Bluetooth)
        {
            return new BluetoothGarminLink(bluetoothDeviceName);
        }
        return new TcpGarminLink(manualHost, manualPort);
    }

    private string DescribeTarget()
    {
        return transport == GarminTransport.Bluetooth
            ? $"藍牙裝置 \"{bluetoothDeviceName}\"（來源：Inspector 設定）"
            : $"{manualHost}:{manualPort}（來源：手動設定）";
    }

    /// <summary>
    /// 首次連線與重連走同一段流程，避免兩套路徑行為不一致。
    /// </summary>
    private IEnumerator LifecycleLoop()
    {
        // 協程被例外中止時，finally 仍會執行，確保 _lifecycle 不會卡在已死的 handle 上
        // 而讓 StartLink() 再也無法重啟。
        try
        {
            yield return LifecycleLoopBody();
        }
        finally
        {
            _lifecycle = null;
        }
    }

    private IEnumerator LifecycleLoopBody()
    {
        int attempt = 0;

        while (!_userDisconnected)
        {
            attempt++;
            string attemptLabel = attempt == 1 ? "" : $"（第 {attempt} 次嘗試）";
            Debug.Log($"{LogPrefix} 連線中 → {DescribeTarget()}{attemptLabel}");

            _link = CreateLink();
            _protocolMismatchReported = false;
            _link.BeginConnect();

            float elapsed = 0f;
            while (_link.State == GarminLinkState.Connecting && elapsed < connectTimeoutSec)
            {
                _link.Poll();
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (_link.State != GarminLinkState.Connected)
            {
                string reason = _link.State == GarminLinkState.Connecting
                    ? $"逾時（{connectTimeoutSec} 秒）"
                    : _link.LastError;
                Debug.LogWarning($"{LogPrefix} 連線失敗：{reason}");
                _link.Dispose();
                _link = null;

                if (_userDisconnected)
                {
                    break;
                }
                yield return new WaitForSecondsRealtime(reconnectIntervalSec);
                continue;
            }

            Debug.Log($"{LogPrefix} 已連線 → {DescribeTarget()}");
            attempt = 0;

            // 重連後補送 start，否則對面不會再送資料。
            // 但若佇列裡已經有尚未送出的 start（使用者在連線建立前就按了 start），
            // 那則指令已經承載了同樣的意圖，補送會變成送兩次。
            string resume = _session.BuildResumeCommand();
            if (!string.IsNullOrEmpty(resume) && !HasPendingStartCommand())
            {
                Debug.Log($"{LogPrefix} 補送 \"{resume}\" 以恢復串流");
                _outgoing.Enqueue(resume);
            }

            yield return ReceiveLoop();

            if (_userDisconnected)
            {
                _link?.Dispose();
                _link = null;
                break;
            }

            Debug.LogWarning($"{LogPrefix} 連線中斷：{_link?.LastError ?? "未知原因"}");
            _link?.Dispose();
            _link = null;

            Debug.Log($"{LogPrefix} {reconnectIntervalSec} 秒後重連");
            yield return new WaitForSecondsRealtime(reconnectIntervalSec);
        }
    }

    private IEnumerator ReceiveLoop()
    {
        float sinceLastData = 0f;
        float sinceLastCommand = commandGapSec;
        bool noDataWarned = false;

        while (_link != null && _link.State == GarminLinkState.Connected && !_userDisconnected)
        {
            _link.Poll();

            // 指令一次只送一則且彼此隔開，避免 Android 端把相鄰指令當成一則解析
            sinceLastCommand += Time.unscaledDeltaTime;
            if (_outgoing.Count > 0 && sinceLastCommand >= commandGapSec)
            {
                // 先 Peek 再送，成功才移除。送失敗的指令留在佇列裡，
                // 否則像 stop 這種沒有其他恢復機制的指令會直接消失。
                string command = _outgoing.Peek();
                if (_link.Send(command))
                {
                    _outgoing.Dequeue();
                    Debug.Log($"{LogPrefix} 已送出：{command}");
                    sinceLastCommand = 0f;
                }
                else
                {
                    Debug.LogWarning($"{LogPrefix} 送出失敗，保留於佇列待重試：{command}");
                }
            }

            bool gotAny = false;
            while (_link.TryReadLine(out string line))
            {
                gotAny = true;
                HandleLine(line);
            }

            if (gotAny)
            {
                sinceLastData = 0f;
                noDataWarned = false;
            }
            else
            {
                sinceLastData += Time.unscaledDeltaTime;
                if (!noDataWarned && sinceLastData >= noDataWarningSec)
                {
                    noDataWarned = true;
                    if (_session.Streaming)
                    {
                        Debug.LogWarning(
                            $"{LogPrefix} 已連線並已送出 start，但 {noDataWarningSec} 秒內" +
                            "沒有收到任何資料。請確認 Android 端正在串流。");
                    }
                    else
                    {
                        Debug.LogWarning(
                            $"{LogPrefix} 已連線但尚未送出 start，因此不會有任何資料。" +
                            "送出 start 後 Android 端才會開始傳送。");
                    }
                }
            }

            yield return null;
        }
    }

    private void HandleLine(string line)
    {
        GarminParseResult result = LabGarminParser.TryParse(line, out LabGarminData data);

        switch (result)
        {
            case GarminParseResult.Ok:
                labGarminData = data;
                try
                {
                    // WriteData 會同步觸發 WriteDataAction，那是開放給實驗腳本訂閱的委派。
                    // 訂閱者拋例外不該讓整條接收協程死掉——協程一旦中止，connectionStatus
                    // 仍會回報 true，現場沒有任何訊號可循。
                    LabDataManager.Instance.WriteData(data);
                }
                catch (Exception e)
                {
                    Debug.LogError($"{LogPrefix} 寫入資料時發生例外（資料已解析成功，" +
                                   $"問題出在 LabDataManager 或其訂閱者）：{e}");
                }
                return;

            case GarminParseResult.Empty:
                return;

            case GarminParseResult.Malformed:
                Debug.LogWarning($"{LogPrefix} 無法解析的資料，已丟棄：{Truncate(line)}");
                return;

            case GarminParseResult.ProtocolMismatch:
                if (!_protocolMismatchReported)
                {
                    _protocolMismatchReported = true;
                    Debug.LogError(
                        $"{LogPrefix} 協定不符 —— 收到的不是 LabGarmin 格式（缺少 time 欄位）。" +
                        "請將 Android 端 Transmit 分頁切到 LabGarmin (legacy)。" +
                        $"收到的內容：{Truncate(line)}");
                }
                return;
        }
    }

    private static string Truncate(string text)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= 120)
        {
            return text;
        }
        return text.Substring(0, 120) + "…";
    }
}
