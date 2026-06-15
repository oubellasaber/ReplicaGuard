using System.Net;
using ReplicaGuard.Core.Abstractions;
using ReplicaGuard.Core.HosterAccounts;
using ReplicaGuard.Core.Hosters;

namespace ReplicaGuard.Infrastructure.Hosters.SendCm.Upload;

internal static class SendCmUploadErrors
{
    private const string Code = nameof(HosterCode.SendCm);

    public static Error InvalidJsonResponse(string detail) =>
        new Error($"Hoster.{Code}.Upload.InvalidJson", "The upload response contained invalid JSON.")
            .WithDetail(detail)
            .WithType(ErrorType.Failure)
            .AsPermanent();
    public static Error EmptyFileCode() =>
        new Error($"Hoster.{Code}.Upload.EmptyFileCode", "The server returned an empty file code.")
            .WithType(ErrorType.Failure)
            .AsPermanent();

    public static Error FileBannedByAdministrator() =>
        new Error($"Hoster.{Code}.Upload.FileBanned", "The file is banned by the hoster administrator.")
            .WithType(ErrorType.Forbidden)
            .AsPermanent();

    public static Error DuplicateLimitReached() =>
        new Error($"Hoster.{Code}.Upload.DuplicateLimitReached", "The file reached the maximum duplicate limit. Upload a unique file.")
            .WithType(ErrorType.Conflict)
            .AsPermanent();

    public static Error MissingSessionId() =>
        new Error($"Hoster.{Code}.Upload.MissingSessionId", "Failed to retrieve session ID from the server.")
            .WithType(ErrorType.Failure)
            .AsPermanent();

    public static Error MissingUploadServer() =>
        new Error($"Hoster.{Code}.Upload.MissingUploadServer", "Failed to retrieve upload server URL.")
            .WithType(ErrorType.Failure)
            .AsPermanent();

    public static Error InvalidUpdateStatFormat() =>
        new Error($"Hoster.{Code}.Upload.InvalidUpdateStat", "Failed to parse update_stat() response.")
            .WithType(ErrorType.Failure)
            .AsPermanent();

    public static Error HttpFailure(string url, HttpStatusCode status) =>
        new Error($"Hoster.{Code}.Upload.HttpFailure", $"The server returned a non‑success status code.")
            .WithType(ErrorType.Failure)
            .WithMetadata(nameof(url), url)
            .WithMetadata("status_code", (int)status);

    public static Error IdentityNotVerified(Guid accountId, Guid identityId) =>
        new Error($"Hoster.{Code}.Upload.IdentityNotVerified", "The hoster account identity is not verified.")
            .WithType(ErrorType.Forbidden)
            .WithMetadata(nameof(accountId), accountId)
            .WithMetadata(nameof(identityId), identityId);

    public static Error IdentityMissing(Guid accountId, IdentityType identityType) =>
        new Error($"Hoster.{Code}.Upload.IdentityMissing", "The hoster account is missing a required identity.")
            .WithType(ErrorType.Forbidden)
            .WithMetadata(nameof(accountId), accountId)
            .WithMetadata(nameof(identityType), identityType.ToString());
}
