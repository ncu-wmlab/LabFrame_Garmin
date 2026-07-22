using System;

/// <summary>可選的傳輸方式。依規格 §6.3，由設定明確指定，不自動切換。</summary>
public enum GarminTransport
{
    Tcp,
    Bluetooth,
}

/// <summary>連線的狀態。</summary>
public enum GarminLinkState
{
    Disconnected,
    Connecting,
    Connected,
    Failed,
}

/// <summary>
/// 與 Android 端之間的一條連線。TCP 與藍牙各一份實作，上層邏輯不需知道走哪條。
/// </summary>
public interface IGarminLink : IDisposable
{
    GarminLinkState State { get; }

    /// <summary>State 為 Failed 時的原因，供 log 使用。</summary>
    string LastError { get; }

    /// <summary>發動連線。不阻塞，之後靠 Poll 推進並觀察 State。</summary>
    void BeginConnect();

    /// <summary>每幀呼叫一次：推進連線狀態、把已抵達的資料抽進緩衝、偵測斷線。</summary>
    void Poll();

    /// <summary>送出一則指令。未連線時回傳 false。</summary>
    bool Send(string command);

    /// <summary>取出一行已收到的資料。沒有完整一行時回傳 false。</summary>
    bool TryReadLine(out string line);
}
