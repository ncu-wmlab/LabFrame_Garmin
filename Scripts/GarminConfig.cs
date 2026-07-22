using System;

/// <summary>
/// Garmin 接收端的連線設定。
///
/// 透過 <c>LabTools.GetConfig&lt;GarminConfig&gt;()</c> 讀取，實際檔案位置依平台不同：
/// <list type="bullet">
/// <item>Editor：<c>Assets/Resources/Config/GarminConfig.json</c>（消費端專案自己的）</item>
/// <item>Windows build：<c>{遊戲資料夾}/Config/GarminConfig.json</c></item>
/// <item>Android / Quest：<c>{persistentDataPath}/Config/GarminConfig.json</c></item>
/// </list>
///
/// 設定刻意放在專案層級而非套件內的 prefab 上：套件由 scoped registry 安裝時會落在
/// <c>Library/PackageCache</c>，那是唯讀的，改不了 prefab；而且每個專案、每次實驗的
/// 平板位址都不同，不該共用套件裡的同一份值。
///
/// 在 Quest 上更換平板時，直接推新的設定檔即可，不需要重新建置：
/// <code>
/// adb push GarminConfig.json /sdcard/Android/data/&lt;包名&gt;/files/Config/
/// </code>
/// </summary>
[Serializable]
public class GarminConfig
{
    /// <summary>
    /// 傳輸方式："Tcp" 或 "Bluetooth"。大小寫不拘。
    /// 刻意用字串而非 enum：JsonUtility 會把 enum 序列化成數字，手改設定檔時看不懂。
    /// 無法辨識的值會回退到 Tcp 並發出警告。
    /// </summary>
    public string Transport;

    /// <summary>Android 平板的區網 IPv4（App 的 Transmit 分頁上會顯示）。</summary>
    public string Host;

    /// <summary>TCP 埠，預設 6001。</summary>
    public int Port;

    /// <summary>藍牙裝置名稱。僅在 Transport 為 Bluetooth 時使用，不得有寫死的預設值。</summary>
    public string BluetoothDeviceName;

    /// <summary>發動連線後等待連線成立的上限秒數。</summary>
    public float ConnectTimeoutSec;

    /// <summary>斷線後每隔幾秒重試一次。重試沒有次數上限。</summary>
    public float ReconnectIntervalSec;

    /// <summary>已連線但持續多久沒有資料就發出警告。</summary>
    public float NoDataWarningSec;

    /// <summary>
    /// 相鄰兩則指令的最小間隔秒數。
    /// Android 端 CommandServer 讀的是原始 chunk 而非逐行，兩則指令若落在同一個
    /// read chunk 會被當成一則解析（後者遺失，或 tag 被汙染），所以必須隔開。
    /// </summary>
    public float CommandGapSec;

    public GarminConfig()
    {
        Transport = "Tcp";
        Host = "";
        Port = 6001;
        BluetoothDeviceName = "";
        ConnectTimeoutSec = 10f;
        ReconnectIntervalSec = 2f;
        NoDataWarningSec = 5f;
        CommandGapSec = 0.15f;
    }

    /// <summary>
    /// 把 <see cref="Transport"/> 字串解析成列舉。無法辨識時回傳 Tcp 並輸出說明。
    /// </summary>
    public GarminTransport ResolveTransport(out string warning)
    {
        warning = null;
        string value = (Transport ?? string.Empty).Trim();

        if (value.Equals("Tcp", StringComparison.OrdinalIgnoreCase))
        {
            return GarminTransport.Tcp;
        }

        if (value.Equals("Bluetooth", StringComparison.OrdinalIgnoreCase))
        {
            return GarminTransport.Bluetooth;
        }

        warning = $"設定檔的 Transport 值 \"{Transport}\" 無法辨識，" +
                  "只接受 \"Tcp\" 或 \"Bluetooth\"，本次以 Tcp 執行。";
        return GarminTransport.Tcp;
    }
}
