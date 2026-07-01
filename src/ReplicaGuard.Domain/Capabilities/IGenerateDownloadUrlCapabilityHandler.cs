namespace ReplicaGuard.Core.Capabilities;

public sealed record DownloadFileRequest(Uri Url);

public sealed record DownloadFileResponse(Uri DownloadUrl, Dictionary<string, string> RequiredHeaders);

public interface IGenerateDownloadUrlCapabilityHandler : ICapabilityHandler<DownloadFileRequest, DownloadFileResponse>;
