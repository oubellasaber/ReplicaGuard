namespace ReplicaGuard.Infrastructure.Recovery;

public sealed class ExpirationRefreshOptions
{
    public static readonly string SectionName = "ExpirationRefresh";

    /// How often the worker runs (minutes). Default 360 = 6 hours.
    public int IntervalMinutes { get; init; } = 360;

    /// How far ahead to scan for replicas near expiry (days). Default 3.
    public int ScanWindowDays { get; init; } = 3;

    /// Threshold to mark replica as ExpiringSoon (days). Default 1.
    public int ExpiringSoonThresholdDays { get; init; } = 1;

    /// Max replicas per batch. Default 10.
    public int BatchSize { get; init; } = 10;

    /// Base minutes for recovery backoff Default 30.
    public int RecoveryBackoffBaseMinutes { get; init; } = 720;
}
