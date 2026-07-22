/// <summary>藍牙 SPP 傳輸。實作於 Task 8 補齊。</summary>
public sealed class BluetoothGarminLink : IGarminLink
{
    private readonly string _deviceName;

    public GarminLinkState State { get; private set; } = GarminLinkState.Disconnected;
    public string LastError { get; private set; }

    public BluetoothGarminLink(string deviceName)
    {
        _deviceName = deviceName;
    }

    public void BeginConnect()
    {
        LastError = "藍牙傳輸尚未實作";
        State = GarminLinkState.Failed;
    }

    public void Poll() { }

    public bool Send(string command) => false;

    public bool TryReadLine(out string line)
    {
        line = null;
        return false;
    }

    public void Dispose()
    {
        State = GarminLinkState.Disconnected;
    }
}
