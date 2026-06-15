namespace ReplicaGuard.Application.Replication.UploadReplica.Spooling;

public interface ISpoolLeaseService
{
    Task<SpoolLease?> TryAcquireAsync(Guid assetId, Guid replicaId, TimeSpan ttl, CancellationToken ct);
    void Renew(SpoolLease lease, TimeSpan ttl);
    void Release(SpoolLease lease);
    Task ReleaseForAsset(Guid assetId);
    Task<SpoolLease?> GetAsync(Guid assetId, CancellationToken ct);
}

public sealed class SpoolLease
{
    public Guid Id { get; set; }
    public Guid AssetId { get; set; }
    public Guid OwnerReplicaId { get;  set; }
    public DateTime ExpiresAtUtc { get; set; }
    public uint Version { get; set; }

    private SpoolLease() { }

    public SpoolLease(Guid assetId, Guid ownerReplicaId, DateTime expiresAtUtc)
    {
        Id = Guid.NewGuid();
        AssetId = assetId;
        OwnerReplicaId = ownerReplicaId;
        ExpiresAtUtc = expiresAtUtc;
        Version = 1;
    }
}
