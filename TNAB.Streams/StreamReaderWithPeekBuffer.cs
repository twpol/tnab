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

    public char Peek()
    {
        if (!EnsureBuffer(1)) throw new InvalidOperationException("End of stream");
        return Buffer[BufferPosition];
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
        if (Peek(text, comparisonType))
        {
            BufferPosition += text.Length;
            return true;
        }
        return false;
    }

    public void ReadUntil(StringBuilder? buffer, params char[] chars)
    {
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
            buffer?.Append(Buffer, BufferPosition, length);
            BufferPosition += length;
        }
    }

    public void ReadWhile(StringBuilder? buffer, params char[] chars)
    {
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
            buffer?.Append(Buffer, BufferPosition, length);
            BufferPosition += length;
        }
    }
}
