using System;
using System.Collections;
using System.Collections.Generic;
using LabFrame2023;
using UnityEngine;

/// <summary>
/// 連到 Android 端的 Garmin Streaming App 接收生理資料。
/// 預設走 TCP；藍牙 SPP 為可選傳輸，由設定檔明確指定，不會自動切換。
///
/// 連線設定來自 <see cref="GarminConfig"/>（專案層級的 JSON 檔），不是 Inspector 欄位——
/// 本套件由 scoped registry 安裝時會落在唯讀的 Library/PackageCache，prefab 改不了，
/// 而且每個專案的平板位址都不同，不該共用套件裡的同一份值。
/// </summary>
public class GarminBluetoothManager : LabSingleton<GarminBluetoothManager>, IManager
{
    private const string LogPrefix = "[GarminBluetoothManager]";

    private GarminConfig _config;

    /// <summary>
    /// 連線設定。ManagerInit 尚未執行時（例如場景直接掛著本元件而 LabFrame 還沒起來）
    /// 會就地載入，避免拿到 null。
    /// </summary>
    private GarminConfig Config
    {
        get
        {
            if (_config == null)
            {
                LoadConfig();
            }
            return _config;
        }
    }

    private readonly GarminSessionState _session = new GarminSessionState();
    private readonly Queue<string> _outgoing = new Queue<string>();

    private IGarminLink _link;
    private Coroutine _lifecycle;
    private bool _userDisconnected;
    private bool _protocolMismatchReported;

    /// <summary>
    /// 使用者是否主動送出過 stop。用來區分「從未開始」與「刻意停止」——
    /// 兩者的 Streaming 都是 false，但只有前者該提醒「你還沒送 start」，
    /// 後者沒有資料本來就是預期結果，不該發警告。
    /// </summary>
    private bool _stoppedByUser;

    /// <summary>目前的資料。</summary>
    public LabGarminData labGarminData = new LabGarminData();

    /// <summary>目前是否已連線。</summary>
    public bool connectionStatus => _link != null && _link.State == GarminLinkState.Connected;

    /// <summary>是否偵測到協定不符（Android 端多半停在 JSONL）。資料全數不可用。</summary>
    public bool ProtocolMismatchDetected => _protocolMismatchReported;

    /* -------------------------------------------------------------------------- */

    public void ManagerInit()
    {
        LoadConfig();
    }

    private void LoadConfig()
    {
        try
        {
            _config = LabTools.GetConfig<GarminConfig>();
        }
        catch (Exception e)
        {
            Debug.LogError($"{LogPrefix} 讀取 GarminConfig 失敗，改用預設值：{e.Message}");
        }

        if (_config == null)
        {
            _config = new GarminConfig();
        }

        GarminTransport resolved = _config.ResolveTransport(out string warning);
        if (warning != null)
        {
            Debug.LogWarning($"{LogPrefix} {warning}");
        }

        Debug.Log($"{LogPrefix} 設定載入完成：transport={resolved}、目標={DescribeTarget()}。" +
                  $"設定檔位置：{LabTools.GetConfigPath<GarminConfig>()}");

        if (resolved == GarminTransport.Tcp && string.IsNullOrWhiteSpace(_config.Host))
        {
            Debug.LogWarning($"{LogPrefix} 設定檔的 Host 是空的，連線會直接失敗。" +
                             "請填入 Android 平板的 IP，或以 SetTcpTarget() 於執行期指定。");
        }
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
                _stoppedByUser = true;
                // 尚未連線時不該為了送 stop 而建立連線
                if (_lifecycle != null)
                {
                    EnqueueCommand(command);
                }
                return;

            default:
                if (kind == GarminCommandKind.Start)
                {
                    _stoppedByUser = false;
                }
                // start 與 tag 都要讓對面知道，必要時順帶把連線帶起來
                StartLink();
                EnqueueCommand(command);
                return;
        }
    }

    /// <summary>
    /// 以程式指定 TCP 連線目標，覆蓋設定檔的值。要在 connect 之前呼叫；
    /// 連線建立後才改只會影響下一次連線。
    /// </summary>
    public void SetTcpTarget(string host, int port)
    {
        Config.Host = host;
        Config.Port = port;
        Debug.Log($"{LogPrefix} TCP 目標設為 {host}:{port}（以程式指定，覆蓋設定檔）");
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
        if (Config.ResolveTransport(out _) == GarminTransport.Bluetooth)
        {
            return new BluetoothGarminLink(Config.BluetoothDeviceName);
        }
        return new TcpGarminLink(Config.Host, Config.Port);
    }

    private string DescribeTarget()
    {
        return Config.ResolveTransport(out _) == GarminTransport.Bluetooth
            ? $"藍牙裝置 \"{Config.BluetoothDeviceName}\"（來源：設定檔）"
            : $"{Config.Host}:{Config.Port}（來源：設定檔）";
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
            while (_link.State == GarminLinkState.Connecting && elapsed < Config.ConnectTimeoutSec)
            {
                _link.Poll();
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (_link.State != GarminLinkState.Connected)
            {
                string reason = _link.State == GarminLinkState.Connecting
                    ? $"逾時（{Config.ConnectTimeoutSec} 秒）"
                    : _link.LastError;
                Debug.LogWarning($"{LogPrefix} 連線失敗：{reason}");
                _link.Dispose();
                _link = null;

                if (_userDisconnected)
                {
                    break;
                }
                yield return new WaitForSecondsRealtime(Config.ReconnectIntervalSec);
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

            Debug.Log($"{LogPrefix} {Config.ReconnectIntervalSec} 秒後重連");
            yield return new WaitForSecondsRealtime(Config.ReconnectIntervalSec);
        }
    }

    private IEnumerator ReceiveLoop()
    {
        float sinceLastData = 0f;
        float sinceLastCommand = Config.CommandGapSec;
        bool noDataWarned = false;
        bool receivedDataLogged = false;

        while (_link != null && _link.State == GarminLinkState.Connected && !_userDisconnected)
        {
            _link.Poll();

            // 指令一次只送一則且彼此隔開，避免 Android 端把相鄰指令當成一則解析
            sinceLastCommand += Time.unscaledDeltaTime;
            if (_outgoing.Count > 0 && sinceLastCommand >= Config.CommandGapSec)
            {
                // 先 Peek 再送，成功才移除。送失敗的指令留在佇列裡，
                // 否則像 stop 這種沒有其他恢復機制的指令會直接消失。
                string command = _outgoing.Peek();
                bool sent = _link.Send(command);

                // 不論成敗都重設計時器：失敗時若不重設，會變成每幀重試並每幀印一次
                // 警告。實務上 Send 失敗多半伴隨狀態轉為 Failed 而使迴圈立刻結束，
                // 但不該依賴那個巧合。
                sinceLastCommand = 0f;

                if (sent)
                {
                    _outgoing.Dequeue();
                    Debug.Log($"{LogPrefix} 已送出：{command}");
                }
                else
                {
                    Debug.LogWarning($"{LogPrefix} 送出失敗，保留於佇列待重試：{command}");
                }
            }

            bool gotAny = false;
            bool gotRecord = false;
            while (_link.TryReadLine(out string line))
            {
                gotAny = true;
                if (HandleLine(line))
                {
                    gotRecord = true;
                }
            }

            // 每條連線報一次。成功接收若完全不出聲，Console 會一片安靜，
            // 使用者無從分辨「正在正常接收」與「根本沒連上」——實際踩過這個坑。
            // 條件必須是「真的解析出一筆紀錄」而非「收到一行」：協定不符時
            // 每秒都會收到行卻沒有一筆能用，這時宣告成功會與上面的錯誤自相矛盾。
            if (gotRecord && !receivedDataLogged)
            {
                receivedDataLogged = true;
                Debug.Log($"{LogPrefix} 已開始接收資料（每秒一筆，之後不再逐筆記錄）。" +
                          $"首筆：heartRate={labGarminData.heartRate}、" +
                          $"SPO2={labGarminData.SPO2}、time={labGarminData.time}");
            }

            if (gotAny)
            {
                sinceLastData = 0f;
                noDataWarned = false;
            }
            else
            {
                sinceLastData += Time.unscaledDeltaTime;
                // 使用者主動 stop 之後沒有資料是預期結果，不該發警告
                if (!noDataWarned && !_stoppedByUser && sinceLastData >= Config.NoDataWarningSec)
                {
                    noDataWarned = true;
                    if (_session.Streaming)
                    {
                        Debug.LogWarning(
                            $"{LogPrefix} 已連線並已送出 start，但 {Config.NoDataWarningSec} 秒內" +
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

    /// <summary>
    /// 處理一行資料。回傳是否真的解析出一筆可用的紀錄——「收到一行」不等於
    /// 「收到資料」，協定不符時每秒都會收到行，卻沒有任何一筆能用。
    /// </summary>
    private bool HandleLine(string line)
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
                return true;

            case GarminParseResult.Empty:
                return false;

            case GarminParseResult.Malformed:
                Debug.LogWarning($"{LogPrefix} 無法解析的資料，已丟棄：{Truncate(line)}");
                return false;

            case GarminParseResult.ProtocolMismatch:
                if (!_protocolMismatchReported)
                {
                    _protocolMismatchReported = true;
                    Debug.LogError(
                        $"{LogPrefix} 協定不符 —— 收到的不是 LabGarmin 格式（缺少 time 欄位）。" +
                        "請將 Android 端 Transmit 分頁切到 LabGarmin (legacy)。" +
                        $"收到的內容：{Truncate(line)}");
                }
                return false;

            default:
                return false;
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
