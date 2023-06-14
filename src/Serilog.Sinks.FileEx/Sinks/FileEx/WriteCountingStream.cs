namespace Serilog.Sinks.FileEx;

internal sealed class WriteCountingStream : Stream
{
    private readonly Stream _stream;

    public WriteCountingStream(Stream stream)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        CountedLength = stream.Length;
    }

    public long CountedLength { get; private set; }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _stream.Dispose();

        base.Dispose(disposing);
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        _stream.Write(buffer, offset, count);
        CountedLength += count;
    }

    public override void Flush() => _stream.Flush();
    public override bool CanRead => false;
    public override bool CanSeek => _stream.CanSeek;
    public override bool CanWrite => true;
    public override long Length => _stream.Length;


    public override long Position
    {
        get => _stream.Position;
        set => throw new NotSupportedException();
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new InvalidOperationException($"Seek operations are not available through `{nameof(WriteCountingStream)}`.");
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }
}