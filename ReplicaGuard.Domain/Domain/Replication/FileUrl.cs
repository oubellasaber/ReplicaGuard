using ReplicaGuard.Core.Abstractions;

namespace ReplicaGuard.Core.Domain.Replication;

/// <summary>
/// Value object representing a validated file URL.
/// </summary>
public sealed record FileUrl
{
    public Uri Value { get; }

    private FileUrl(Uri value) => Value = value;

    public static Result<FileUrl> Create(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return Result.Failure<FileUrl>(ReplicationErrors.FileUrlEmpty);

        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
            return Result.Failure<FileUrl>(ReplicationErrors.FileUrlInvalid(url));

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            return Result.Failure<FileUrl>(ReplicationErrors.FileUrlUnsupportedScheme(uri.Scheme));

        return new FileUrl(uri);
    }

    public override string ToString() => Value.ToString();

    public static implicit operator string(FileUrl fileUrl) => fileUrl.Value.ToString();
}
