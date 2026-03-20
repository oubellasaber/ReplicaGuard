using ReplicaGuard.Core.Abstractions;

namespace ReplicaGuard.Infrastructure.Hosters.SendCm;

internal static class SendCmUploadErrors
{
    public static Error InvalidJsonResponse(string detail) =>
        new Error("Hoster.SendCm.Upload.InvalidJson", "The upload response contained invalid JSON.")
            .WithDetail(detail)
            .WithType(ErrorType.Failure);

    public static Error EmptyFileCode() =>
        new Error("Hoster.SendCm.Upload.EmptyFileCode", "The server returned an empty file code.")
            .WithType(ErrorType.Failure);

    public static Error MissingSessionId() =>
        new Error("Hoster.SendCm.Upload.MissingSessionId", "Failed to retrieve session ID from the server.")
            .WithType(ErrorType.Failure);

    public static Error MissingUploadServer() =>
        new Error("Hoster.SendCm.Upload.MissingUploadServer", "Failed to retrieve upload server URL.")
            .WithType(ErrorType.Failure);

    public static Error InvalidUpdateStatFormat() =>
        new Error("Hoster.SendCm.Upload.InvalidUpdateStat", "Failed to parse update_stat() response.")
            .WithType(ErrorType.Failure);
}
