using ReplicaGuard.Core.Abstractions;

namespace ReplicaGuard.Infrastructure.Hosters.Pixeldrain;

internal static class PixeldrainUploadErrors
{
    public static Error NoFile() =>
        new Error("Hoster.Pixeldrain.Upload.NoFile", "The file does not exist or is empty.")
            .WithMetadata("StatusCode", 422)
            .WithType(ErrorType.Validation);

    public static Error FileTooLarge() =>
        new Error("Hoster.Pixeldrain.Upload.FileTooLarge", "The file exceeds the maximum allowed size.")
            .WithMetadata("StatusCode", 413)
            .WithType(ErrorType.InvalidInput);

    public static Error NameTooLong() =>
        new Error("Hoster.Pixeldrain.Upload.NameTooLong", "File name exceeds 255 characters.")
            .WithMetadata("StatusCode", 413)
            .WithType(ErrorType.InvalidInput);

    public static Error WritingError() =>
        new Error("Hoster.Pixeldrain.Upload.WritingError", "Failed to write file to server storage.")
            .WithDetail("The server may be out of storage space.")
            .WithMetadata("StatusCode", 500)
            .WithType(ErrorType.Failure);

    public static Error InternalServerError() =>
        new Error("Hoster.Pixeldrain.Upload.InternalError", "The server encountered an internal error.")
            .WithMetadata("StatusCode", 500)
            .WithType(ErrorType.Failure);

    public static Error UnknownError(int statusCode, string detail) =>
        new Error("Hoster.Pixeldrain.Upload.Unknown", "An unknown error occurred during upload.")
            .WithDetail(detail)
            .WithMetadata("StatusCode", statusCode)
            .WithType(ErrorType.Failure);
}
