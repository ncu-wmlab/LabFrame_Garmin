using NUnit.Framework;

public class GarminSessionStateTests
{
    [Test]
    public void connect_不改變串流狀態()
    {
        var state = new GarminSessionState();

        Assert.AreEqual(GarminCommandKind.Connect, state.Interpret("connect"));
        Assert.IsFalse(state.Streaming);
    }

    [Test]
    public void start_開啟串流且無tag()
    {
        var state = new GarminSessionState();

        Assert.AreEqual(GarminCommandKind.Start, state.Interpret("start"));
        Assert.IsTrue(state.Streaming);
        Assert.IsNull(state.Tag);
    }

    [Test]
    public void 帶逗號的start_取出tag()
    {
        var state = new GarminSessionState();

        Assert.AreEqual(GarminCommandKind.Start, state.Interpret("start,T01"));
        Assert.IsTrue(state.Streaming);
        Assert.AreEqual("T01", state.Tag);
    }

    [Test]
    public void stop_關閉串流但保留tag()
    {
        var state = new GarminSessionState();
        state.Interpret("start,T01");

        Assert.AreEqual(GarminCommandKind.Stop, state.Interpret("stop"));
        Assert.IsFalse(state.Streaming);
        Assert.AreEqual("T01", state.Tag);
    }

    [Test]
    public void disconnect_關閉串流()
    {
        var state = new GarminSessionState();
        state.Interpret("start");

        Assert.AreEqual(GarminCommandKind.Disconnect, state.Interpret("disconnect"));
        Assert.IsFalse(state.Streaming);
    }

    [Test]
    public void 任意字串_視為tag且不影響串流狀態()
    {
        var state = new GarminSessionState();
        state.Interpret("start,T01");

        Assert.AreEqual(GarminCommandKind.Tag, state.Interpret("T02"));
        Assert.AreEqual("T02", state.Tag);
        Assert.IsTrue(state.Streaming, "送 tag 不應影響串流狀態");
    }

    [Test]
    public void 未串流時_沒有補送指令()
    {
        var state = new GarminSessionState();

        Assert.IsNull(state.BuildResumeCommand());
    }

    [Test]
    public void 串流中無tag_補送start()
    {
        var state = new GarminSessionState();
        state.Interpret("start");

        Assert.AreEqual("start", state.BuildResumeCommand());
    }

    [Test]
    public void 串流中有tag_補送帶tag的start()
    {
        var state = new GarminSessionState();
        state.Interpret("start,T01");

        Assert.AreEqual("start,T01", state.BuildResumeCommand());
    }

    // 實驗中會以裸 tag 指令切換階段，補送時必須帶上最新的 tag 而非 start 當下那個
    [Test]
    public void start之後又送裸tag_補送指令要用最新的tag()
    {
        var state = new GarminSessionState();
        state.Interpret("start,T01");
        state.Interpret("T02");

        Assert.AreEqual("start,T02", state.BuildResumeCommand());
    }

    [Test]
    public void stop之後_沒有補送指令()
    {
        var state = new GarminSessionState();
        state.Interpret("start,T01");
        state.Interpret("stop");

        Assert.IsNull(state.BuildResumeCommand());
    }

    [Test]
    public void 前後空白會被去掉()
    {
        var state = new GarminSessionState();

        Assert.AreEqual(GarminCommandKind.Start, state.Interpret("  start,T01  "));
        Assert.AreEqual("T01", state.Tag);
    }

    [Test]
    public void 結尾逗號的start_清空tag而非沿用舊值()
    {
        var state = new GarminSessionState();
        state.Interpret("start,T01");

        state.Interpret("start,");

        Assert.AreEqual(string.Empty, state.Tag);
        Assert.AreEqual("start", state.BuildResumeCommand(),
            "tag 已被清空，補送指令不可夾帶先前的 T01");
    }

    [Test]
    public void null與空字串_視為tag且不拋例外()
    {
        var state = new GarminSessionState();

        Assert.AreEqual(GarminCommandKind.Tag, state.Interpret(null));
        Assert.AreEqual(string.Empty, state.Tag);

        Assert.AreEqual(GarminCommandKind.Tag, state.Interpret(""));
        Assert.AreEqual(string.Empty, state.Tag);
    }

    [Test]
    public void disconnect之後_保留tag但沒有補送指令()
    {
        var state = new GarminSessionState();
        state.Interpret("start,T01");

        state.Interpret("disconnect");

        Assert.AreEqual("T01", state.Tag);
        Assert.IsNull(state.BuildResumeCommand());
    }
}
