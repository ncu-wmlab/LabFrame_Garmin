using System;
using System.Linq;
using UnityEngine;

/// <summary>
/// 藍牙 SPP 傳輸，包裝既有的 BluetoothManager plugin。
/// 僅 Android/Quest 可用；plugin 的 Windows 分支全部拋 PlatformNotSupportedException，
/// 因此非 Android 平台一律在 BeginConnect 就明確失敗，不讓例外在執行期才炸出來。
/// </summary>
public sealed class BluetoothGarminLink : IGarminLink
{
    private const float DiscoverySeconds = 3f;

    private readonly string _deviceName;

    private float _discoveryElapsed;
    private bool _connectIssued;

    public GarminLinkState State { get; private set; } = GarminLinkState.Disconnected;

    public string LastError { get; private set; }

    public BluetoothGarminLink(string deviceName)
    {
        _deviceName = deviceName;
    }

    public void BeginConnect()
    {
#if !UNITY_ANDROID
        Fail("此平台不支援藍牙（plugin 只有 Android 實作），請將 transport 改為 Tcp");
        return;
#else
        if (string.IsNullOrWhiteSpace(_deviceName))
        {
            Fail("尚未設定藍牙裝置名稱");
            return;
        }

        if (BluetoothManager.Instance == null)
        {
            Fail("BluetoothManager 尚未就緒");
            return;
        }

        try
        {
            BluetoothManager.Instance.CheckPermission();
            BluetoothManager.Instance.StartDiscovery();
            _discoveryElapsed = 0f;
            _connectIssued = false;
            State = GarminLinkState.Connecting;
            LastError = null;
        }
        catch (Exception e)
        {
            Fail($"啟動藍牙搜尋失敗 — {e.Message}");
        }
#endif
    }

    public void Poll()
    {
#if UNITY_ANDROID
        if (State == GarminLinkState.Connecting)
        {
            AdvanceConnecting();
        }
        else if (State == GarminLinkState.Connected && !BluetoothManager.Instance.IsConnected())
        {
            Fail("藍牙連線已中斷");
        }
#endif
    }

#if UNITY_ANDROID
    private void AdvanceConnecting()
    {
        try
        {
            if (!_connectIssued)
            {
                _discoveryElapsed += Time.unscaledDeltaTime;
                if (_discoveryElapsed < DiscoverySeconds)
                {
                    return;
                }

                var devices = BluetoothManager.Instance.GetAvailableDevices();
                var target = devices.FirstOrDefault(d => d.name == _deviceName);
                if (target.name == null)
                {
                    Fail($"找不到藍牙裝置 \"{_deviceName}\"");
                    return;
                }

                if (!BluetoothManager.Instance.Connect(target.mac, ""))
                {
                    Fail($"連線到 \"{_deviceName}\"({target.mac}) 失敗");
                    return;
                }

                _connectIssued = true;
                return;
            }

            if (BluetoothManager.Instance.IsConnected())
            {
                State = GarminLinkState.Connected;
                LastError = null;
            }
        }
        catch (Exception e)
        {
            Fail($"藍牙連線過程失敗 — {e.Message}");
        }
    }
#endif

    public bool Send(string command)
    {
#if !UNITY_ANDROID
        return false;
#else
        if (State != GarminLinkState.Connected)
        {
            return false;
        }

        try
        {
            // 與 TcpGarminLink 一致附加換行。plugin 不會自己加，
            // 而兩則指令若黏在一起，沒有分隔符會比有分隔符更難解析。
            return BluetoothManager.Instance.Send(command + "\n");
        }
        catch (Exception e)
        {
            Fail($"送出指令失敗 — {e.Message}");
            return false;
        }
#endif
    }

    public bool TryReadLine(out string line)
    {
        line = null;
#if !UNITY_ANDROID
        return false;
#else
        if (State != GarminLinkState.Connected)
        {
            return false;
        }

        try
        {
            if (BluetoothManager.Instance.Available() <= 0)
            {
                return false;
            }

            // plugin 的 ReadLine 由 Java 端負責切行。已知風險：Available() > 0 但尚未
            // 收滿一整行時，其行為未經驗證，可能阻塞。TCP 路徑沒有這個問題。
            line = BluetoothManager.Instance.ReadLine();
            return line != null;
        }
        catch (Exception e)
        {
            Fail($"讀取資料失敗 — {e.Message}");
            return false;
        }
#endif
    }

    private void Fail(string reason)
    {
        LastError = reason;
        State = GarminLinkState.Failed;
    }

    public void Dispose()
    {
#if UNITY_ANDROID
        try
        {
            if (BluetoothManager.Instance != null && BluetoothManager.Instance.IsConnected())
            {
                BluetoothManager.Instance.Stop();
            }
        }
        catch (Exception)
        {
            // 關閉失敗不影響後續重連，忽略
        }
#endif
        State = GarminLinkState.Disconnected;
    }
}
