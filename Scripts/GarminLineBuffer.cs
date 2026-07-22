using System.Text;

/// <summary>
/// 把串流中陸續抵達的 byte 切成一行一行的字串。
/// 使用具狀態的 UTF-8 Decoder，多位元組字元被切在 chunk 邊界時不會解碼失敗。
/// </summary>
public sealed class GarminLineBuffer
{
    private readonly Decoder _decoder = Encoding.UTF8.GetDecoder();
    private readonly StringBuilder _pending = new StringBuilder();
    private char[] _charBuffer = new char[1024];

    /// <summary>
    /// 累積 buffer 前 count 個 byte。
    /// </summary>
    public void Append(byte[] buffer, int count)
    {
        if (buffer == null || count <= 0)
        {
            return;
        }

        // 解碼器可能還留著上次未成字的 byte，故上限抓 count + 2
        int required = count + 2;
        if (_charBuffer.Length < required)
        {
            _charBuffer = new char[required];
        }

        int charCount = _decoder.GetChars(buffer, 0, count, _charBuffer, 0, false);
        _pending.Append(_charBuffer, 0, charCount);
    }

    /// <summary>
    /// 取出一行（不含換行字元）。尚無完整一行時回傳 false。
    /// </summary>
    public bool TryReadLine(out string line)
    {
        for (int i = 0; i < _pending.Length; i++)
        {
            if (_pending[i] != '\n')
            {
                continue;
            }

            int length = i;
            if (length > 0 && _pending[length - 1] == '\r')
            {
                length--;
            }

            line = _pending.ToString(0, length);
            _pending.Remove(0, i + 1);
            return true;
        }

        line = null;
        return false;
    }
}
