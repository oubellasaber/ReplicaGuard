using ReplicaGuard.Core.Abstractions;

namespace ReplicaGuard.Infrastructure.Hosters;

internal static class HosterErrors
{
    public static Error LocalFileNotFound(string filePath) =>
        new Error(
            code: "Hoster.Upload.LocalFile.NotFound",
            message: $"The specified local file was not found"
        )
        .WithMetadata("FilePath", filePath)
        .AsPermanent();
}
