using System;
using UnityEngine;

/// <summary>
/// 解析單行資料的結果。
/// </summary>
public enum GarminParseResult
{
    /// <summary>解析成功。</summary>
    Ok,
    /// <summary>空行，略過即可，不算錯誤。</summary>
    Empty,
    /// <summary>不是合法 JSON。</summary>
    Malformed,
    /// <summary>是合法 JSON，但不是 LabGarmin 格式（多半是 Android 端停在 JSONL）。</summary>
    ProtocolMismatch,
}

/// <summary>
/// 把 Android 端送來的一行文字解析成 <see cref="LabGarminData"/>。
/// </summary>
public static class LabGarminParser
{
    public static GarminParseResult TryParse(string line, out LabGarminData data)
    {
        data = null;

        if (string.IsNullOrWhiteSpace(line))
        {
            return GarminParseResult.Empty;
        }

        LabGarminData parsed;
        try
        {
            parsed = JsonUtility.FromJson<LabGarminData>(line.Trim());
        }
        catch (Exception)
        {
            return GarminParseResult.Malformed;
        }

        if (parsed == null)
        {
            return GarminParseResult.Malformed;
        }

        // 協定守門。JsonUtility 遇到欄位完全不同的 JSON 不會拋例外，而是忽略未知欄位、
        // 缺漏欄位留預設值，回傳一個全零物件。若 Android 端停在 JSONL 模式送
        // ExperimentRecord（與本型別只有 tag 一個欄位重疊），沒有這道關卡就會靜默寫入
        // 整場全 0 的資料。LabGarminFormatter 一定會輸出 time，ExperimentRecord 沒有這個欄位。
        if (!LooksLikeLabGarminTime(parsed.time))
        {
            return GarminParseResult.ProtocolMismatch;
        }

        data = parsed;
        return GarminParseResult.Ok;
    }

    /// <summary>
    /// LabGarminFormatter 固定輸出 yyyy-MM-dd HH-mm-ss-SSS，一定同時含有 '-' 與 ' '。
    /// 只檢查非空是不夠的：JsonUtility 會把 {"time":12345} 靜默轉成字串 "12345"、
    /// {"time":true} 轉成 "true"，兩者都非空卻不是 LabGarmin 的時間格式。
    /// 這裡不比對完整格式，避免 Android 端調整毫秒位數就誤判。
    /// </summary>
    private static bool LooksLikeLabGarminTime(string time)
    {
        if (string.IsNullOrWhiteSpace(time))
        {
            return false;
        }

        return time.IndexOf('-') >= 0 && time.IndexOf(' ') >= 0;
    }
}
