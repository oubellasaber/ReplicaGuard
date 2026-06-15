using ReplicaGuard.Core.Abstractions;

namespace ReplicaGuard.Application.Replication.UploadReplica;

public static class UploadReplicaErrors
{
    public static Error UploadNotSupported(string hosterCode) =>
        new Error(
            code: "HosterCode.Upload.NotSupported",
            message: "The selected hoster does not support uploading capability."
        )
        .WithMetadata("Code", hosterCode)
        .AsPermanent();

    public static Error NoCredentials(string hosterCode) =>
        new Error(
            code: "HosterCode.Upload.Credentials.Missing",
            message: "No credentials were provided for the hoster."
        )
        .WithMetadata("Code", hosterCode)
        .AsPermanent();

    public static Error LocalFileNotFound(string filePath) =>
        new Error(
            code: "HosterCode.Upload.LocalFile.NotFound",
            message: $"The specified local file was not found"
        )
        .WithMetadata("FilePath", filePath)
        .AsPermanent();

    public static Error DownloaderDisappeared = new Error(
        code: "DownloaderDisappeared",
        message: "Downloader vanished before waiting could be registered."
    )
    .AsPermanent();
}
