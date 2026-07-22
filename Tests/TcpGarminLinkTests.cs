using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using NUnit.Framework;

public class TcpGarminLinkTests
{
    private TcpListener _listener;
    private TcpClient _serverSide;

    [SetUp]
    public void SetUp()
    {
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
    }

    [TearDown]
    public void TearDown()
    {
        _serverSide?.Close();
        _serverSide = null;
        _listener?.Stop();
        _listener = null;
    }

    private int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

    /// <summary>推進 link 直到條件成立或逾時，模擬每幀呼叫 Poll。</summary>
    private static bool PumpUntil(IGarminLink link, Func<bool> condition, int timeoutMs = 3000)
    {
        var watch = Stopwatch.StartNew();
        while (watch.ElapsedMilliseconds < timeoutMs)
        {
            link.Poll();
            if (condition())
            {
                return true;
            }
            Thread.Sleep(5);
        }
        return false;
    }

    private void AcceptServerSide()
    {
        _serverSide = _listener.AcceptTcpClient();
    }

    private void ServerSend(string text)
    {
        byte[] payload = Encoding.UTF8.GetBytes(text);
        _serverSide.GetStream().Write(payload, 0, payload.Length);
        _serverSide.GetStream().Flush();
    }

    [Test]
    public void 連上loopback後_狀態變成Connected()
    {
        using (var link = new TcpGarminLink("127.0.0.1", Port))
        {
            link.BeginConnect();
            AcceptServerSide();

            Assert.IsTrue(PumpUntil(link, () => link.State == GarminLinkState.Connected),
                "應在逾時前進入 Connected，實際為 " + link.State + " / " + link.LastError);
        }
    }

    [Test]
    public void 收到一整行_可取出()
    {
        using (var link = new TcpGarminLink("127.0.0.1", Port))
        {
            link.BeginConnect();
            AcceptServerSide();
            PumpUntil(link, () => link.State == GarminLinkState.Connected);

            ServerSend("{\"heartRate\":72}\n");

            string line = null;
            Assert.IsTrue(PumpUntil(link, () => link.TryReadLine(out line)));
            Assert.AreEqual("{\"heartRate\":72}", line);
        }
    }

    [Test]
    public void 送出的指令_伺服端收得到()
    {
        using (var link = new TcpGarminLink("127.0.0.1", Port))
        {
            link.BeginConnect();
            AcceptServerSide();
            PumpUntil(link, () => link.State == GarminLinkState.Connected);

            Assert.IsTrue(link.Send("start,T01"));

            var buffer = new byte[256];
            int read = _serverSide.GetStream().Read(buffer, 0, buffer.Length);
            string received = Encoding.UTF8.GetString(buffer, 0, read).Trim();
            Assert.AreEqual("start,T01", received);
        }
    }

    [Test]
    public void 伺服端關閉連線_狀態轉為Failed()
    {
        using (var link = new TcpGarminLink("127.0.0.1", Port))
        {
            link.BeginConnect();
            AcceptServerSide();
            PumpUntil(link, () => link.State == GarminLinkState.Connected);

            _serverSide.Close();
            _serverSide = null;

            Assert.IsTrue(PumpUntil(link, () => link.State == GarminLinkState.Failed),
                "對方關閉連線後應偵測到並轉為 Failed，否則自動重連不會被觸發");
        }
    }

    [Test]
    public void 連不到的位址_狀態轉為Failed且有錯誤訊息()
    {
        // port 1 幾乎不可能有服務在聽
        using (var link = new TcpGarminLink("127.0.0.1", 1))
        {
            link.BeginConnect();

            Assert.IsTrue(PumpUntil(link, () => link.State == GarminLinkState.Failed, 5000));
            Assert.IsNotEmpty(link.LastError);
        }
    }

    [Test]
    public void 位址為空_立刻失敗且不嘗試連線()
    {
        using (var link = new TcpGarminLink("", 6001))
        {
            link.BeginConnect();

            Assert.AreEqual(GarminLinkState.Failed, link.State);
            Assert.IsNotEmpty(link.LastError);
        }
    }

    [Test]
    public void 未連線時送指令_回傳false()
    {
        using (var link = new TcpGarminLink("127.0.0.1", Port))
        {
            Assert.IsFalse(link.Send("start"));
        }
    }
}
