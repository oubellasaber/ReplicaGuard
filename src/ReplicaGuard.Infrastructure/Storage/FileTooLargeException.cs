namespace ReplicaGuard.Infrastructure.Storage;

public sealed class FileTooLargeException : Exception
{
    public long SizeBytes { get; }
    public long LimitBytes { get; }

    public FileTooLargeException(long sizeBytes, long limitBytes)
        : base($"File size {sizeBytes:N0} bytes exceeds limit of {limitBytes:N0} bytes.")
    {
        SizeBytes = sizeBytes;
        LimitBytes = limitBytes;
    }
}
