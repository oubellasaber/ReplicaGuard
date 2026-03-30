using ReplicaGuard.Core.Abstractions;

namespace ReplicaGuard.Infrastructure.Hosters.SendCm;

internal static class SendCmFileErrors
{
    public static Error RenameInvalidResponse(string detail) =>
        new Error("Hoster.SendCm.Rename.InvalidResponse", "The rename response was invalid.")
            .WithDetail(detail)
            .WithType(ErrorType.Failure);

    public static Error RenameBadRequest() =>
        new Error("Hoster.SendCm.Rename.BadRequest", "Rename request is invalid.")
            .WithType(ErrorType.InvalidInput);

    public static Error RenameForbidden() =>
        new Error("Hoster.SendCm.Rename.Forbidden", "Rename not allowed for the provided credentials.")
            .WithType(ErrorType.Forbidden);

    public static Error RenameFileNotFound(string fileCode) =>
        new Error("Hoster.SendCm.Rename.FileNotFound", "File was not found on hoster.")
            .WithDetail($"File code '{fileCode}' was not found.")
            .WithType(ErrorType.NotFound);

    public static Error RenameUnavailable() =>
        new Error("Hoster.SendCm.Rename.Unavailable", "File rename is unavailable.")
            .WithType(ErrorType.Failure);

    public static Error RenameFailed(string detail) =>
        new Error("Hoster.SendCm.Rename.Failed", "File rename failed.")
            .WithDetail(detail)
            .WithType(ErrorType.Failure);
}
