using System.Net;
using ReplicaGuard.Core.Abstractions;
using ReplicaGuard.Core.Domain.Replication;

namespace ReplicaGuard.Infrastructure.Hosters;

internal static class HosterUploadErrors
{
    public static Error UploadFailed(string hosterCode, UploadMethod method, HttpStatusCode statusCode) =>
        new Error("Hoster.Upload.Failed", "File upload failed.")
            .WithDetail($"The upload request returned HTTP {(int)statusCode} {statusCode}.")
            .WithMetadata("HosterCode", hosterCode)
            .WithMetadata("Method", method.ToString())
            .WithMetadata("StatusCode", (int)statusCode)
            .WithType(MapStatusCodeToErrorType(statusCode));

    public static Error UploadFailed(string hosterCode, string detail) =>
        new Error("Hoster.Upload.Failed", "File upload failed due to an unexpected error.")
            .WithDetail(detail)
            .WithMetadata("HosterCode", hosterCode)
            .WithType(ErrorType.Failure);

    public static Error UploadMethodNotSupported(string hosterCode, UploadMethod method) =>
        new Error("Hoster.Upload.MethodNotSupported", "The upload method is not supported by this hoster.")
            .WithMetadata("HosterCode", hosterCode)
            .WithMetadata("Method", method.ToString())
            .WithType(ErrorType.InvalidInput);

    public static Error InvalidResponse(string hosterCode, string detail) =>
        new Error("Hoster.Upload.InvalidResponse", "The hoster returned an invalid or unexpected response.")
            .WithDetail(detail)
            .WithMetadata("HosterCode", hosterCode)
            .WithType(ErrorType.Failure);

    private static ErrorType MapStatusCodeToErrorType(HttpStatusCode statusCode) =>
        statusCode switch
        {
            HttpStatusCode.BadRequest => ErrorType.Validation,
            HttpStatusCode.Unauthorized => ErrorType.Unauthorized,
            HttpStatusCode.Forbidden => ErrorType.Forbidden,
            HttpStatusCode.NotFound => ErrorType.NotFound,
            HttpStatusCode.Conflict => ErrorType.Conflict,
            HttpStatusCode.UnprocessableEntity => ErrorType.Validation,
            HttpStatusCode.RequestEntityTooLarge => ErrorType.InvalidInput,
            _ => ErrorType.Failure
        };
}
