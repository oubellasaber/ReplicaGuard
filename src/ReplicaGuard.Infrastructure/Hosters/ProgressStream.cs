using ReplicaGuard.Domain.Capabilities;

namespace ReplicaGuard.Infrastructure.Hosters;

internal sealed class ProgressStream : Stream
{
    private readonly Stream _inner;
    private readonly Action<TransferProgress>? _onProgress;
    private readonly bool _leaveOpen;
    private long _totalRead;

    public ProgressStream(
        Stream inner,
        Action<TransferProgress>? onProgress,
        bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(inner);

        _inner = inner;
        _onProgress = onProgress;
        _leaveOpen = leaveOpen;
    }

    private void ReportProgress(int bytesRead)
    {
        if (bytesRead <= 0)
            return;

        _totalRead += bytesRead;

        _onProgress?.Invoke(new TransferProgress(
            BytesTransferred: _totalRead));
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        int bytesRead = _inner.Read(buffer, offset, count);
        ReportProgress(bytesRead);
        return bytesRead;
    }

    public override async Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken)
    {
        int bytesRead = await _inner.ReadAsync(
            buffer.AsMemory(offset, count),
            cancellationToken);

        ReportProgress(bytesRead);
        return bytesRead;
    }

    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        int bytesRead = await _inner.ReadAsync(buffer, cancellationToken);
        ReportProgress(bytesRead);
        return bytesRead;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_leaveOpen)
        {
            _inner.Dispose();
        }

        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        if (!_leaveOpen)
        {
            await _inner.DisposeAsync();
        }

        await base.DisposeAsync();
    }

    public override bool CanRead => _inner.CanRead;
    public override bool CanSeek => _inner.CanSeek;
    public override bool CanWrite => _inner.CanWrite;
    public override long Length => _inner.Length;

    public override long Position
    {
        get => _inner.Position;
        set => _inner.Position = value;
    }

    public override void Flush() => _inner.Flush();

    public override Task FlushAsync(CancellationToken cancellationToken) =>
        _inner.FlushAsync(cancellationToken);

    public override long Seek(long offset, SeekOrigin origin) =>
        _inner.Seek(offset, origin);

    public override void SetLength(long value) =>
        _inner.SetLength(value);

    public override void Write(byte[] buffer, int offset, int count) =>
        _inner.Write(buffer, offset, count);

    public override Task WriteAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken) =>
        _inner.WriteAsync(buffer.AsMemory(offset, count), cancellationToken)
            .AsTask();
}
