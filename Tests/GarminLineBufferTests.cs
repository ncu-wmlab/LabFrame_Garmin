using System.Text;
using NUnit.Framework;

public class GarminLineBufferTests
{
    private static byte[] Bytes(string s) => Encoding.UTF8.GetBytes(s);

    private static void Feed(GarminLineBuffer buffer, byte[] data)
    {
        buffer.Append(data, data.Length);
    }

    [Test]
    public void 完整一行_可取出且不含換行()
    {
        var buffer = new GarminLineBuffer();
        Feed(buffer, Bytes("{\"heartRate\":72}\n"));

        Assert.IsTrue(buffer.TryReadLine(out string line));
        Assert.AreEqual("{\"heartRate\":72}", line);
    }

    [Test]
    public void 尚未收到換行_不回傳任何行()
    {
        var buffer = new GarminLineBuffer();
        Feed(buffer, Bytes("{\"heartRate\":72}"));

        Assert.IsFalse(buffer.TryReadLine(out _));
    }

    [Test]
    public void 一次抵達兩行_可連續取出兩行()
    {
        var buffer = new GarminLineBuffer();
        Feed(buffer, Bytes("first\nsecond\n"));

        Assert.IsTrue(buffer.TryReadLine(out string a));
        Assert.AreEqual("first", a);
        Assert.IsTrue(buffer.TryReadLine(out string b));
        Assert.AreEqual("second", b);
        Assert.IsFalse(buffer.TryReadLine(out _));
    }

    [Test]
    public void CRLF_結尾的_CR_會被去掉()
    {
        var buffer = new GarminLineBuffer();
        Feed(buffer, Bytes("value\r\n"));

        Assert.IsTrue(buffer.TryReadLine(out string line));
        Assert.AreEqual("value", line);
    }

    [Test]
    public void 空行_回傳空字串()
    {
        var buffer = new GarminLineBuffer();
        Feed(buffer, Bytes("\n"));

        Assert.IsTrue(buffer.TryReadLine(out string line));
        Assert.AreEqual(string.Empty, line);
    }

    [Test]
    public void 多位元組字元被切在chunk邊界_仍能正確還原()
    {
        // 「實」的 UTF-8 是 3 個 byte，切在第 1 個 byte 之後
        byte[] all = Bytes("{\"tag\":\"實驗\"}\n");
        const int split = 9;

        var head = new byte[split];
        System.Array.Copy(all, 0, head, 0, split);
        var tail = new byte[all.Length - split];
        System.Array.Copy(all, split, tail, 0, all.Length - split);

        var buffer = new GarminLineBuffer();
        buffer.Append(head, head.Length);
        Assert.IsFalse(buffer.TryReadLine(out _), "只收到半個字元時不應吐出整行");

        buffer.Append(tail, tail.Length);
        Assert.IsTrue(buffer.TryReadLine(out string line));
        Assert.AreEqual("{\"tag\":\"實驗\"}", line);
    }

    [Test]
    public void 逐byte餵入_仍能組出完整一行()
    {
        byte[] all = Bytes("{\"tag\":\"實驗\"}\n");
        var buffer = new GarminLineBuffer();

        for (int i = 0; i < all.Length - 1; i++)
        {
            buffer.Append(new[] { all[i] }, 1);
            Assert.IsFalse(buffer.TryReadLine(out _));
        }

        buffer.Append(new[] { all[all.Length - 1] }, 1);
        Assert.IsTrue(buffer.TryReadLine(out string line));
        Assert.AreEqual("{\"tag\":\"實驗\"}", line);
    }

    [Test]
    public void 四位元組字元被切在chunk邊界_仍能正確還原()
    {
        // U+1F600 是 4-byte UTF-8、UTF-16 需要一組 surrogate pair 表示。
        // 這是 Append 容量計算最吃緊的路徑：解碼器暫存 3 個 byte 後，
        // 下一次僅 1 個 byte 就要一次吐出 2 個 char。
        const string emoji = "\U0001F600";
        byte[] all = Bytes("{\"tag\":\"" + emoji + "\"}\n");

        // 切在 emoji 四個 byte 的第 3 個之後，讓解碼器暫存 3 個 byte
        const int split = 11;

        var head = new byte[split];
        System.Array.Copy(all, 0, head, 0, split);
        var tail = new byte[all.Length - split];
        System.Array.Copy(all, split, tail, 0, all.Length - split);

        var buffer = new GarminLineBuffer();
        buffer.Append(head, head.Length);
        Assert.IsFalse(buffer.TryReadLine(out _));

        buffer.Append(tail, tail.Length);
        Assert.IsTrue(buffer.TryReadLine(out string line));
        Assert.AreEqual("{\"tag\":\"" + emoji + "\"}", line);
    }
}
