namespace ReplicaGuard.Infrastructure.Storage;

public sealed class CountingStream : Stream
{
    private readonly Stream _inner;
    private readonly long _maxBytes;
    private long _totalBytesWritten;

    public long TotalBytesWritten => _totalBytesWritten;

    public CountingStream(Stream inner, long maxBytes)
    {
        _inner = inner;
        _maxBytes = maxBytes;
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        _totalBytesWritten += count;
        EnsureLimit();
        _inner.Write(buffer, offset, count);
    }

    public override async Task WriteAsync(
        byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        _totalBytesWritten += count;
        EnsureLimit();
        await _inner.WriteAsync(buffer.AsMemory(offset, count), cancellationToken);
    }

    public override async ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        _totalBytesWritten += buffer.Length;
        EnsureLimit();
        await _inner.WriteAsync(buffer, cancellationToken);
    }

    private void EnsureLimit()
    {
        if (_totalBytesWritten > _maxBytes)
            throw new FileTooLargeException(_totalBytesWritten, _maxBytes);
    }

    public override bool CanRead => _inner.CanRead;
    public override bool CanSeek => _inner.CanSeek;
    public override bool CanWrite => _inner.CanWrite;
    public override long Length => _inner.Length;
    public override long Position { get => _inner.Position; set => _inner.Position = value; }
    public override void Flush() => _inner.Flush();
    public override Task FlushAsync(CancellationToken cancellationToken) => _inner.FlushAsync(cancellationToken);
    public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct) => _inner.ReadAsync(buffer, offset, count, ct);
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct) => _inner.ReadAsync(buffer, ct);
    public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
    public override void SetLength(long value) => _inner.SetLength(value);

    protected override void Dispose(bool disposing)
    {
        if (disposing) _inner.Dispose();
        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        await _inner.DisposeAsync();
        await base.DisposeAsync();
    }
}
