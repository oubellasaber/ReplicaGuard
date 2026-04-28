namespace ReplicaGuard.Core.Domain.Replication.Planner;

public sealed record HosterCapabilities(string Code, bool SupportsSpooledUpload, bool SupportsRemoteFetch);
public sealed record SpoolState(SpoolStatus Status, string FilePath, Guid? OwnerReplicaId);

public enum SpoolStatus
{
    NotExist = 0,
    Downloading = 1,
    Completed = 2
}

public abstract record UploadDecision
{
    public sealed record NoOp(string Reason) : UploadDecision;
    public sealed record FailPermanent(string Reason) : UploadDecision;

    public sealed record WaitForPeer(Guid PeerId, TimeSpan Timeout) : UploadDecision;

    public sealed record DownloadToSpool(RemoteFileSource Source) : UploadDecision;

    public sealed record UploadFromRemote(RemoteFileSource Source) : UploadDecision;
    public sealed record UploadFromLocal(string FilePath) : UploadDecision;
    public sealed record WaitForDownload() : UploadDecision;

    public static UploadDecision NoOpDecision(string reason) => new NoOp(reason);
    public static UploadDecision FailPermanentDecision(string reason) => new FailPermanent(reason);
    public static UploadDecision WaitForPeerDecision(Guid peerId, TimeSpan timeout) => new WaitForPeer(peerId, timeout);
    public static UploadDecision DownloadToSpoolDecision(RemoteFileSource source) => new DownloadToSpool(source);
    public static UploadDecision UploadFromRemoteDecision(RemoteFileSource source) => new UploadFromRemote(source);
    public static UploadDecision UploadFromLocalDecision(string path) => new UploadFromLocal(path);
    public static UploadDecision WaitForDownloadDecision() => new WaitForDownload();
}


public interface IUploadPlanner
{
    UploadDecision Plan(Asset asset, Guid replicaId, HosterCapabilities caps, SpoolState spool);
}

public sealed class UploadPlanner : IUploadPlanner
{
    public UploadDecision Plan(Asset asset, Guid replicaId, HosterCapabilities caps, SpoolState spool)
    {
        var replica = asset.Replicas.First(r => r.Id == replicaId);

        if (replica.IsTerminal) return UploadDecision.NoOpDecision("Replica.InTerminalState");
        if (asset.Source is null) return UploadDecision.FailPermanentDecision("Asset.NoSource");
        if (!caps.SupportsSpooledUpload && !caps.SupportsRemoteFetch)
            return UploadDecision.FailPermanentDecision("Hoster.Upload.NotSupported");

        if (asset.Source is LocalFileSource local)
            return UploadDecision.UploadFromLocalDecision(local.FilePath);

        var remoteSource = (RemoteFileSource)asset.Source;


        if (caps.SupportsRemoteFetch)
            return UploadDecision.UploadFromRemoteDecision(remoteSource);

        switch (spool.Status)
        {
            case SpoolStatus.NotExist:
                return UploadDecision.DownloadToSpoolDecision(remoteSource);
            case SpoolStatus.Downloading when spool.OwnerReplicaId.HasValue && spool.OwnerReplicaId.Value != replicaId:
                return UploadDecision.WaitForPeerDecision(spool.OwnerReplicaId.Value, TimeSpan.FromSeconds(10));
            case SpoolStatus.Downloading:
                return UploadDecision.WaitForDownloadDecision();
            case SpoolStatus.Completed:
                return UploadDecision.UploadFromLocalDecision(spool.FilePath);
            default:
                return UploadDecision.FailPermanentDecision("Spool is in invalid state");
        }
    }
}
