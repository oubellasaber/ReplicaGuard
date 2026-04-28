namespace ReplicaGuard.Infrastructure.Spool;
public sealed class SpoolLease
{
    public Guid AssetId { get; private set; }
    public Guid OwnerReplicaId { get; internal set; }
    public DateTime ExpiresAtUtc { get; internal set; }
    public uint Version { get; internal set; }

    public SpoolLease(Guid assetId, Guid ownerReplicaId, DateTime expiresAtUtc)
    {
        AssetId = assetId;
        OwnerReplicaId = ownerReplicaId;
        ExpiresAtUtc = expiresAtUtc;
    }
}
