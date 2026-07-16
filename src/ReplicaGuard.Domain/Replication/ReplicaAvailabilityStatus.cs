namespace ReplicaGuard.Domain.Replication;

public enum ReplicaAvailabilityStatus
{
    Unknown = 1,
    Healthy = 2,
    ExpiringSoon = 3,
    Expired = 4,
    Tombstoned = 5,
    Processing = 6
}
