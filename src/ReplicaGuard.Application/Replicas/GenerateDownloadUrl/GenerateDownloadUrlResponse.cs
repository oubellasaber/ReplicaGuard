namespace ReplicaGuard.Application.Replicas.GenerateDownloadUrl;

public sealed record GenerateDownloadUrlResponse(Uri DownloadUrl, Dictionary<string, string> RequiredHeaders);