using System.Diagnostics;
using System.Text;

namespace TNAB.Streams;

public class StreamReaderWithPeekBuffer(Stream stream)
{
    readonly StreamReader StreamReader = new(stream);

    readonly char[] Buffer = new char[1024];
    int BufferPosition;
    int BufferLength;
    int BufferConsumed;
    bool StreamReaderEndOfStream;

    public int Position { get => BufferConsumed + BufferPosition; }

    void FillBuffer()
    {
        if (StreamReaderEndOfStream) return;
        while (BufferLength < Buffer.Length)
        {
            var read = StreamReader.Read(Buffer, BufferLength, Buffer.Length - BufferLength);
            if (read == 0)
            {
                StreamReaderEndOfStream = true;
                break;
            }
            BufferLength += read;
        }
    }

    void UpdateBuffer()
    {
        Array.Copy(Buffer, BufferPosition, Buffer, 0, Buffer.Length - BufferPosition);
        BufferConsumed += BufferPosition;
        BufferLength -= BufferPosition;
        BufferPosition = 0;
    }

    bool EnsureBuffer(int count)
    {
        Debug.Assert(count <= Buffer.Length, "Cannot read more than buffer size at once");
        if (BufferPosition + count > BufferLength)
        {
            if (BufferPosition + count > Buffer.Length) UpdateBuffer();
            FillBuffer();
        }
        return BufferPosition + count <= BufferLength;
    }

    public bool EndOfStream { get => BufferPosition >= BufferLength && StreamReaderEndOfStream; }

    public ReadOnlySpan<char> PeekLength(int length)
    {
        return Buffer.AsSpan(BufferPosition, Math.Min(BufferLength - BufferPosition, length));
    }

    public char Peek() => Peek(0);

    public char Peek(int offset)
    {
        if (!EnsureBuffer(1 + offset)) throw new InvalidOperationException("End of stream");
        return Buffer[BufferPosition + offset];
    }

    public char Read()
    {
        if (!EnsureBuffer(1)) throw new InvalidOperationException("End of stream");
        return Buffer[BufferPosition++];
    }

    public bool Peek(string text, StringComparison comparisonType = StringComparison.Ordinal)
    {
        if (!EnsureBuffer(text.Length)) return false;
        return 0 == new ReadOnlySpan<char>(Buffer, BufferPosition, text.Length).CompareTo(text, comparisonType);
    }

    public bool Read(string text, StringComparison comparisonType = StringComparison.Ordinal)
    {
        return Read(null, text, comparisonType);
    }

    public bool Read(StringBuilder? buffer, string text, StringComparison comparisonType = StringComparison.Ordinal)
    {
        if (Peek(text, comparisonType))
        {
            Html5NormalisingCopy(buffer, Buffer, BufferPosition, text.Length);
            BufferPosition += text.Length;
            return true;
        }
        return false;
    }

    public void ReadUntil(StringBuilder? buffer, params char[] chars)
    {
        Debug.Assert(chars.Contains('\r') == chars.Contains('\n'), $"{nameof(ReadUntil)} parameter {nameof(chars)} must contain both or neither of CR and LF for HTML5 normalisation to succeed");
        while (!EndOfStream)
        {
            var length = 0;
            while (length < Buffer.Length)
            {
                if (!EnsureBuffer(length + 1)) break;
                if (chars.Contains(Buffer[BufferPosition + length])) break;
                length++;
            }
            if (length == 0) break;
            Html5NormalisingCopy(buffer, Buffer, BufferPosition, length);
            BufferPosition += length;
        }
    }

    public void ReadWhile(StringBuilder? buffer, params char[] chars)
    {
        Debug.Assert(chars.Contains('\r') == chars.Contains('\n'), $"{nameof(ReadWhile)} parameter {nameof(chars)} must contain both or neither of CR and LF for HTML5 normalisation to succeed");
        while (!EndOfStream)
        {
            var length = 0;
            while (length < Buffer.Length)
            {
                if (!EnsureBuffer(length + 1)) break;
                if (!chars.Contains(Buffer[BufferPosition + length])) break;
                length++;
            }
            if (length == 0) break;
            Html5NormalisingCopy(buffer, Buffer, BufferPosition, length);
            BufferPosition += length;
        }
    }

    // Spec: https://html.spec.whatwg.org/commit-snapshots/aa0f4e89a9c34273ba2a4b70169cc50b247cb8da/#preprocessing-the-input-stream
    static void Html5NormalisingCopy(StringBuilder? buffer, char[] value, int startIndex, int charCount)
    {
        if (buffer == null) return;
        ArgumentNullException.ThrowIfNull(value, nameof(value));
        ArgumentOutOfRangeException.ThrowIfNegative(startIndex, nameof(startIndex));
        ArgumentOutOfRangeException.ThrowIfNegative(charCount, nameof(charCount));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(startIndex + charCount, value.Length, nameof(charCount));
        if (charCount == 0) return;
        buffer.EnsureCapacity(buffer.Length + charCount);
        for (var index = startIndex; index < startIndex + charCount; index++)
        {
            // Spec: https://infra.spec.whatwg.org/commit-snapshots/283e1b6190a1eeedca573cfa4f4388e6c4c649fe/#normalize-newlines
            if (value[index] == '\r')
            {
                buffer.Append('\n');
                if (index + 1 < value.Length && value[index + 1] == '\n') index++;
            }
            else
            {
                buffer.Append(value[index]);
            }
        }
    }
}
