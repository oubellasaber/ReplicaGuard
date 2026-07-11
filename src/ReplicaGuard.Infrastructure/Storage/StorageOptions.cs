namespace ReplicaGuard.Infrastructure.Storage;

public sealed class StorageOptions
{
    public static readonly string SectionName = "Storage";

    /// Maximum allowed size per file in bytes. Default 10 GB.
    public long MaxFileSizeBytes { get; init; } = 10L * 1024 * 1024 * 1024;

    /// Maximum concurrent spool downloads. Bounds worst-case spool usage.
    public int MaxConcurrentDownloads { get; init; } = 3;

    /// Minimum free disk space to keep as safety buffer.
    public long MinFreeBytes { get; init; } = 500L * 1024 * 1024;

    /// Days to keep completed assets before cleanup.
    public int RetentionDays { get; init; } = 7;

    /// How often the background cleanup service runs (minutes).
    public int CleanupIntervalMinutes { get; init; } = 30;
}
