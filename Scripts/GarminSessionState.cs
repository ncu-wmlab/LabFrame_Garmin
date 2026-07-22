using System;

/// <summary>
/// 上行指令的類別。語意須與 Android 端 BtCommandParser 一致。
/// </summary>
public enum GarminCommandKind
{
    Connect,
    Start,
    Stop,
    Disconnect,
    Tag,
}

/// <summary>
/// 記錄「希望對面處於什麼狀態」。斷線重連後靠這裡的資訊補送指令恢復串流。
/// 比對順序與 Android 端 BtCommandParser 相同：disconnect → connect → start → stop → 其餘為 tag。
/// </summary>
public sealed class GarminSessionState
{
    /// <summary>是否已要求對面送資料。</summary>
    public bool Streaming { get; private set; }

    /// <summary>最後一次生效的 tag，未設定時為 null。</summary>
    public string Tag { get; private set; }

    /// <summary>
    /// 判讀一則要送出的指令並更新內部狀態。
    /// </summary>
    public GarminCommandKind Interpret(string message)
    {
        string trimmed = (message ?? string.Empty).Trim();

        if (trimmed.StartsWith("disconnect", StringComparison.Ordinal))
        {
            Streaming = false;
            return GarminCommandKind.Disconnect;
        }

        if (trimmed.StartsWith("connect", StringComparison.Ordinal))
        {
            return GarminCommandKind.Connect;
        }

        if (trimmed.StartsWith("start", StringComparison.Ordinal))
        {
            Streaming = true;
            // 與 Android 端 BtCommandParser.substringAfterComma 對齊：只要找得到逗號就取其後
            // 的內容，逗號在結尾時得到空字串（代表「清掉 tag」），不可沿用舊值。
            int comma = trimmed.IndexOf(',');
            if (comma >= 0)
            {
                Tag = trimmed.Substring(comma + 1);
            }
            return GarminCommandKind.Start;
        }

        if (trimmed.StartsWith("stop", StringComparison.Ordinal))
        {
            Streaming = false;
            return GarminCommandKind.Stop;
        }

        Tag = trimmed;
        return GarminCommandKind.Tag;
    }

    /// <summary>
    /// 重連後用來恢復串流的指令。未在串流中時回傳 null。
    /// </summary>
    public string BuildResumeCommand()
    {
        if (!Streaming)
        {
            return null;
        }

        return string.IsNullOrEmpty(Tag) ? "start" : "start," + Tag;
    }
}
