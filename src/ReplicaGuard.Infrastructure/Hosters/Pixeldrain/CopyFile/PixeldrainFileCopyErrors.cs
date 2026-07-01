using ReplicaGuard.Domain.Abstractions;

namespace ReplicaGuard.Infrastructure.Hosters.Pixeldrain.CopyFile;

internal static class PixeldrainFileCopyErrors
{
    public static Error InvalidUrl(string url) =>
        new Error("Hoster.Pixeldrain.FileCopy.InvalidUrl", "The provided URL is invalid.")
            .WithDetail($"'{url}' is not a valid pixeldrain URL.")
            .WithType(ErrorType.InvalidInput)
            .AsPermanent();

    public static Error ValidApiKeyIsRequired() =>
        new Error("Hoster.Pixeldrain.FileCopy.ValidApiKeyIsRequired", "A valid api key is required for this operation.")
            .WithDetail($"This request requires API authentication. Please provide an API key in the password field of HTTP Basic Access Authentication")
            .WithType(ErrorType.Unauthorized)
            .AsPermanent();

    public static Error FileWithCodeNotFound(string fileCode) =>
        new Error("Hoster.Pixeldrain.FileCopy.FileNotFound", "The specified file was not found on Pixeldrain.")
            .WithDetail($"No file with code '{fileCode}' was found on Pixeldrain.")
            .WithType(ErrorType.NotFound)
            .AsPermanent();
     public static Error UnknownError(int statusCode, string detail) =>
        new Error("Hoster.Pixeldrain.FileCopy.Unknown", "An unknown error occurred during file copy.")
            .WithDetail(detail)
            .WithMetadata("StatusCode", statusCode)
            .WithType(ErrorType.Failure);
}
