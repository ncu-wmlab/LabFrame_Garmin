using System;
using System.Net.Sockets;
using System.Text;

/// <summary>
/// 以 TCP 連到 Android 端的 Garmin Streaming App（預設 port 6001）。
/// 不需要任何 native plugin，Windows Editor 與 Android 共用同一份程式碼。
/// </summary>
public sealed class TcpGarminLink : IGarminLink
{
    private readonly string _host;
    private readonly int _port;
    private readonly GarminLineBuffer _lineBuffer = new GarminLineBuffer();
    private readonly byte[] _readBuffer = new byte[4096];

    private TcpClient _client;
    private NetworkStream _stream;
    private IAsyncResult _connectResult;

    public GarminLinkState State { get; private set; } = GarminLinkState.Disconnected;

    public string LastError { get; private set; }

    public TcpGarminLink(string host, int port)
    {
        _host = host;
        _port = port;
    }

    public void BeginConnect()
    {
        if (State == GarminLinkState.Connecting || State == GarminLinkState.Connected)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_host))
        {
            Fail("尚未設定連線位址，請在 Inspector 填入 Android 平板的 IP 或呼叫 SetTcpTarget()");
            return;
        }

        try
        {
            _client = new TcpClient();
            // 每則指令自成一個 TCP segment。Android 端 CommandServer 讀的是原始 chunk
            // 而非逐行，兩則指令若被合併送出會被當成一則解析，導致後者遺失。
            _client.NoDelay = true;
            _connectResult = _client.BeginConnect(_host, _port, null, null);
            State = GarminLinkState.Connecting;
            LastError = null;
        }
        catch (Exception e)
        {
            Fail($"無法建立連線至 {_host}:{_port} — {e.Message}");
        }
    }

    public void Poll()
    {
        if (State == GarminLinkState.Connecting)
        {
            AdvanceConnecting();
        }
        else if (State == GarminLinkState.Connected)
        {
            PumpIncoming();
        }
    }

    private void AdvanceConnecting()
    {
        if (_connectResult == null)
        {
            Fail("連線尚未發動");
            return;
        }

        if (!_connectResult.IsCompleted)
        {
            return;
        }

        try
        {
            _client.EndConnect(_connectResult);
            _stream = _client.GetStream();
            State = GarminLinkState.Connected;
            LastError = null;
        }
        catch (Exception e)
        {
            Fail($"無法連線至 {_host}:{_port} — {e.Message}");
        }
        finally
        {
            _connectResult = null;
        }
    }

    private void PumpIncoming()
    {
        try
        {
            // TcpClient.Connected 不會因對方關閉而更新；這是可靠的偵測方式，
            // 沒有它自動重連永遠不會被觸發。
            Socket socket = _client.Client;
            if (socket.Poll(0, SelectMode.SelectRead) && _client.Available == 0)
            {
                Fail("連線已被對方關閉");
                return;
            }

            while (_stream != null && _stream.DataAvailable)
            {
                int read = _stream.Read(_readBuffer, 0, _readBuffer.Length);
                if (read <= 0)
                {
                    Fail("連線已被對方關閉");
                    return;
                }
                _lineBuffer.Append(_readBuffer, read);
            }
        }
        catch (Exception e)
        {
            Fail($"讀取資料失敗 — {e.Message}");
        }
    }

    public bool Send(string command)
    {
        if (State != GarminLinkState.Connected || _stream == null)
        {
            return false;
        }

        try
        {
            byte[] payload = Encoding.UTF8.GetBytes(command + "\n");
            _stream.Write(payload, 0, payload.Length);
            _stream.Flush();
            return true;
        }
        catch (Exception e)
        {
            Fail($"送出指令失敗 — {e.Message}");
            return false;
        }
    }

    public bool TryReadLine(out string line)
    {
        return _lineBuffer.TryReadLine(out line);
    }

    private void Fail(string reason)
    {
        LastError = reason;
        State = GarminLinkState.Failed;
        CloseSocket();
    }

    private void CloseSocket()
    {
        try
        {
            _stream?.Close();
        }
        catch (Exception)
        {
            // 關閉失敗不影響後續重連，忽略
        }

        try
        {
            _client?.Close();
        }
        catch (Exception)
        {
            // 同上
        }

        _stream = null;
        _client = null;
        _connectResult = null;
    }

    public void Dispose()
    {
        CloseSocket();
        State = GarminLinkState.Disconnected;
    }
}
