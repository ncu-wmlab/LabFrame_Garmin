using NUnit.Framework;

public class GarminConfigTests
{
    [Test]
    public void 預設值是可直接使用的Tcp設定()
    {
        var config = new GarminConfig();

        Assert.AreEqual(GarminTransport.Tcp, config.ResolveTransport(out string warning));
        Assert.IsNull(warning);
        Assert.AreEqual(6001, config.Port);
        Assert.AreEqual(0.15f, config.CommandGapSec, 0.0001f);
        Assert.IsEmpty(config.Host, "預設不得指向任何位址，必須由使用者填寫");
        Assert.IsEmpty(config.BluetoothDeviceName, "不得有寫死的裝置名稱");
    }

    [Test]
    public void Tcp與Bluetooth都能解析()
    {
        var config = new GarminConfig { Transport = "Tcp" };
        Assert.AreEqual(GarminTransport.Tcp, config.ResolveTransport(out _));

        config.Transport = "Bluetooth";
        Assert.AreEqual(GarminTransport.Bluetooth, config.ResolveTransport(out _));
    }

    // 設定檔是給人手改的，不該要求記得大小寫
    [Test]
    public void 大小寫不拘()
    {
        var config = new GarminConfig { Transport = "tcp" };
        Assert.AreEqual(GarminTransport.Tcp, config.ResolveTransport(out string w1));
        Assert.IsNull(w1);

        config.Transport = "BLUETOOTH";
        Assert.AreEqual(GarminTransport.Bluetooth, config.ResolveTransport(out string w2));
        Assert.IsNull(w2);
    }

    [Test]
    public void 前後空白會被忽略()
    {
        var config = new GarminConfig { Transport = "  Bluetooth  " };

        Assert.AreEqual(GarminTransport.Bluetooth, config.ResolveTransport(out string warning));
        Assert.IsNull(warning);
    }

    // 手改設定檔打錯字時，不該安靜地走一條使用者沒預期的路
    [Test]
    public void 無法辨識的值回退Tcp並附帶說明()
    {
        var config = new GarminConfig { Transport = "wifi" };

        Assert.AreEqual(GarminTransport.Tcp, config.ResolveTransport(out string warning));
        Assert.IsNotNull(warning, "回退是靜默降級，必須有訊息說明");
        StringAssert.Contains("wifi", warning, "訊息要指出到底填了什麼");
    }

    [Test]
    public void null與空字串也回退Tcp並附帶說明()
    {
        var config = new GarminConfig { Transport = null };
        Assert.AreEqual(GarminTransport.Tcp, config.ResolveTransport(out string w1));
        Assert.IsNotNull(w1);

        config.Transport = "";
        Assert.AreEqual(GarminTransport.Tcp, config.ResolveTransport(out string w2));
        Assert.IsNotNull(w2);
    }
}
