using NUnit.Framework;

public class LabGarminParserTests
{
    private const string ValidLine =
        "{\"heartRate\":72,\"restingHeartRate\":0,\"heartRateVariability\":45," +
        "\"accelerometer_x\":1,\"accelerometer_y\":2,\"accelerometer_z\":3,\"steps\":1234," +
        "\"calories_Total\":0,\"calories_Active\":0,\"floors_Climb\":0,\"floors_Descend\":0," +
        "\"intensityMinutes_Moderate\":0,\"intensityMinutes_Vigorous\":0,\"stressLevel\":30," +
        "\"SPO2\":98,\"bodyBattery\":0,\"respiration\":16," +
        "\"time\":\"2026-07-21 14-30-05-123\",\"tag\":\"T01\"}";

    // Android 端 JSONL 模式送的是 ExperimentRecord，與 LabGarminData 只有 tag 一個欄位重疊
    private const string ExperimentRecordLine =
        "{\"sessionId\":\"S-001\",\"receivedAtEpochMs\":1769000000000," +
        "\"deviceTimestampMs\":1769000000000,\"deviceId\":\"D-01\",\"dataType\":\"HeartRate\"," +
        "\"scalarValue\":72.0,\"callbackSequence\":41,\"connectionState\":\"CONNECTED\"," +
        "\"sdkVersion\":\"1.0\",\"tag\":\"T01\"}";

    [Test]
    public void 正常的LabGarmin一行_解析成功且欄位正確()
    {
        var result = LabGarminParser.TryParse(ValidLine, out LabGarminData data);

        Assert.AreEqual(GarminParseResult.Ok, result);
        Assert.AreEqual(72, data.heartRate);
        Assert.AreEqual(45, data.heartRateVariability);
        Assert.AreEqual(98, data.SPO2);
        Assert.AreEqual("T01", data.tag);
        Assert.AreEqual("2026-07-21 14-30-05-123", data.time);
    }

    [Test]
    public void 空行與純空白_回傳Empty()
    {
        Assert.AreEqual(GarminParseResult.Empty, LabGarminParser.TryParse("", out _));
        Assert.AreEqual(GarminParseResult.Empty, LabGarminParser.TryParse("   ", out _));
        Assert.AreEqual(GarminParseResult.Empty, LabGarminParser.TryParse(null, out _));
    }

    [Test]
    public void 非JSON字串_回傳Malformed()
    {
        Assert.AreEqual(GarminParseResult.Malformed, LabGarminParser.TryParse("not json at all", out _));
    }

    [Test]
    public void 半截JSON_回傳Malformed()
    {
        Assert.AreEqual(GarminParseResult.Malformed,
            LabGarminParser.TryParse("{\"heartRate\":72,\"tim", out _));
    }

    // 這是沒有守門就會靜默通過、寫入整場全零資料的那條路徑
    [Test]
    public void ExperimentRecord格式_被判定為協定不符而非全零資料()
    {
        var result = LabGarminParser.TryParse(ExperimentRecordLine, out LabGarminData data);

        Assert.AreEqual(GarminParseResult.ProtocolMismatch, result);
        Assert.IsNull(data, "協定不符時不得回傳資料物件，避免呼叫端誤寫入");
    }

    [Test]
    public void 缺少time欄位_一律視為協定不符()
    {
        Assert.AreEqual(GarminParseResult.ProtocolMismatch,
            LabGarminParser.TryParse("{\"heartRate\":72,\"tag\":\"T01\"}", out _));
    }

    [Test]
    public void 缺少tag欄位但有time_仍解析成功且tag為null()
    {
        var result = LabGarminParser.TryParse(
            "{\"heartRate\":72,\"time\":\"2026-07-21 14-30-05-123\"}", out LabGarminData data);

        Assert.AreEqual(GarminParseResult.Ok, result);
        Assert.IsNull(data.tag);
    }

    [Test]
    public void time為數字_JsonUtility會轉成字串_仍須判定協定不符()
    {
        Assert.AreEqual(GarminParseResult.ProtocolMismatch,
            LabGarminParser.TryParse("{\"heartRate\":72,\"time\":12345}", out _));
    }

    [Test]
    public void time為布林_仍須判定協定不符()
    {
        Assert.AreEqual(GarminParseResult.ProtocolMismatch,
            LabGarminParser.TryParse("{\"heartRate\":72,\"time\":true}", out _));
    }

    [Test]
    public void time為純空白_仍須判定協定不符()
    {
        Assert.AreEqual(GarminParseResult.ProtocolMismatch,
            LabGarminParser.TryParse("{\"heartRate\":72,\"time\":\"   \"}", out _));
    }
}
