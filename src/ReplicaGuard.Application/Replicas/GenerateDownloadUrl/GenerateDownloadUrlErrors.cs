using ReplicaGuard.Domain.Abstractions;

namespace ReplicaGuard.Application.Replicas.GenerateDownloadUrl;

public static class GenerateDownloadUrlErrors
{
    public static Error ReplicaNotCompleted(Guid replicaId) =>
        new Error("GenerateDownloadUrl.ReplicaNotCompleted",
            "The replica must be in completed status to generate a download URL.",
            ErrorType.InvalidInput)
        .WithMetadata("ReplicaId", replicaId);

    public static Error MissingLink(Guid replicaId) =>
        new Error("GenerateDownloadUrl.MissingLink",
            "The replica has no file link.",
            ErrorType.InvalidInput)
        .WithMetadata("ReplicaId", replicaId);
}