using ReplicaGuard.Core.Abstractions;

namespace ReplicaGuard.Core.Domain.Replication;

/// <summary>
/// Abstract base class representing the source of a file for replication.
/// Can be either a remote URL or a local upload.
/// </summary>
public abstract record FileSource
{
    public FileSourceType SourceType { get; protected init; }

    public abstract bool IsRemote { get; }
    public abstract bool IsLocal { get; }

    protected FileSource(FileSourceType sourceType)
    {
        SourceType = sourceType;
    }
}

/// <summary>
/// Discriminator for file source type.
/// </summary>
public enum FileSourceType
{
    Remote = 0,
    Local = 1
}
