using System.Collections.ObjectModel;
using ReplicaGuard.Core.Abstractions;

namespace ReplicaGuard.Core.Domain.Replication;

/// <summary>
/// Represents a remote file source with URL, custom headers, and optional body.
/// Used when the file needs to be downloaded from a remote location.
/// </summary>
public sealed record RemoteFileSource : FileSource
{
    public FileUrl Url { get; }
    public IReadOnlyDictionary<string, string> Headers { get; }
    public object? Body { get; }

    public override bool IsRemote => true;
    public override bool IsLocal => false;

    private RemoteFileSource(
        FileUrl url,
        IReadOnlyDictionary<string, string> headers,
        object? body) : base(FileSourceType.Remote)
    {
        Url = url;
        Headers = headers;
        Body = body;
    }

    /// <summary>
    /// Creates a remote file source with URL only (no custom headers or body).
    /// </summary>
    public static Result<RemoteFileSource> Create(string url)
    {
        Result<FileUrl> fileUrlResult = FileUrl.Create(url);
        if (fileUrlResult.IsFailure)
            return Result.Failure<RemoteFileSource>(fileUrlResult.Error);

        return new RemoteFileSource(
            fileUrlResult.Value,
            new ReadOnlyDictionary<string, string>(new Dictionary<string, string>()),
            null);
    }

    /// <summary>
    /// Creates a remote file source with URL and custom headers.
    /// </summary>
    public static Result<RemoteFileSource> Create(
        string url,
        IDictionary<string, string> headers)
    {
        Result<FileUrl> fileUrlResult = FileUrl.Create(url);
        if (fileUrlResult.IsFailure)
            return Result.Failure<RemoteFileSource>(fileUrlResult.Error);

        if (headers == null)
            return Result.Failure<RemoteFileSource>(
                ReplicationErrors.HeadersCannotBeNull);

        return new RemoteFileSource(
            fileUrlResult.Value,
            new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(headers)),
            null);
    }

    /// <summary>
    /// Creates a remote file source with URL, custom headers, and body.
    /// </summary>
    public static Result<RemoteFileSource> Create(
        string url,
        IDictionary<string, string> headers,
        object? body)
    {
        Result<FileUrl> fileUrlResult = FileUrl.Create(url);
        if (fileUrlResult.IsFailure)
            return Result.Failure<RemoteFileSource>(fileUrlResult.Error);

        if (headers == null)
            return Result.Failure<RemoteFileSource>(
                ReplicationErrors.HeadersCannotBeNull);

        return new RemoteFileSource(
            fileUrlResult.Value,
            new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(headers)),
            body);
    }

    /// <summary>
    /// Creates a new RemoteFileSource with additional headers merged.
    /// </summary>
    public RemoteFileSource WithHeaders(IDictionary<string, string> additionalHeaders)
    {
        Dictionary<string, string> mergedHeaders = new(Headers);
        foreach (KeyValuePair<string, string> header in additionalHeaders)
        {
            mergedHeaders[header.Key] = header.Value;
        }

        return new RemoteFileSource(
            Url,
            new ReadOnlyDictionary<string, string>(mergedHeaders),
            Body);
    }

    /// <summary>
    /// Creates a new RemoteFileSource with a different body.
    /// </summary>
    public RemoteFileSource WithBody(object? body)
    {
        return new RemoteFileSource(Url, Headers, body);
    }

    public override string ToString() => Url.ToString();
}
