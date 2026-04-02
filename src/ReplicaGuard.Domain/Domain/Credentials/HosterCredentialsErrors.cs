using ReplicaGuard.Core.Abstractions;
using ReplicaGuard.Core.Domain.Common;

namespace ReplicaGuard.Core.Domain.Credentials;

public static class HosterCredentialsErrors
{
    public static Error NotFound(Guid id) =>
        CommonErrors.NotFound("Hoster.Credentials", id);

    public static Error AlreadyExists(string hosterCode) =>
        new Error(
            code: "Hoster.Credentials.AlreadyExists",
            message: "Credentials already exist for this Hoster.")
        .WithDetail(
            $"Credentials for Hoster '{hosterCode}' already exist. " +
            "If you intended to update existing credentials, please use the update endpoint instead.")
        .WithType(ErrorType.Conflict);

    public static Error InvalidCredentialType(string hosterCode) =>
        new Error(
            code: "Hoster.Credentials.InvalidCredentialType",
            message: "The provided credential type does not match the hoster's requirement.")
        .WithDetail(
            $"Hoster '{hosterCode}' requires a different credential type than what was provided.")
        .WithType(ErrorType.Validation);

    public static Error MissingApiKey() =>
        new Error(
            code: "Hoster.Credentials.MissingApiKey",
            message: "API key is required for this hoster.")
        .WithType(ErrorType.Validation);

    public static Error MissingEmail() =>
        new Error(
            code: "Hoster.Credentials.MissingEmail",
            message: "Email is required for this hoster.")
        .WithType(ErrorType.Validation);

    public static Error MissingUsername() =>
        new Error(
            code: "Hoster.Credentials.MissingUsername",
            message: "Username is required for this hoster.")
        .WithType(ErrorType.Validation);

    public static Error MissingPassword() =>
        new Error(
            code: "Hoster.Credentials.MissingPassword",
            message: "Password is required for this hoster.")
        .WithType(ErrorType.Validation);

    public static Error NoCredentialsProvided() =>
        new Error(
            code: "Hoster.Credentials.NoCredentialsProvided",
            message: "At least one credential must be provided for update.")
        .WithType(ErrorType.Validation);

    public static Error ValidationInProgress() =>
        new Error(
            code: "Hoster.Credentials.ValidationInProgress",
            message: "Credentials are currently being validated.")
        .WithDetail(
            "Please wait until the current validation completes before making changes. " +
            "Check the sync status and try again shortly.")
        .WithType(ErrorType.Conflict);

    public static Error VersionMismatch() =>
        new Error(
            code: "Hoster.Credentials.VersionMismatch",
            message: "Credentials were modified during validation.")
        .WithDetail(
            "The credentials were updated while validation was in progress. " +
            "A new validation cycle will process the latest changes.")
        .WithType(ErrorType.Conflict);

    public static Error SecondaryCredentialsMismatch() =>
        new Error(
            code: "Hoster.Credentials.SecondaryCredentialsMismatch",
            message: "Secondary credentials are invalid.")
        .WithDetail(
            "The provided secondary credentials could not be verified as belonging " +
            "to the same account as the primary credentials.")
        .WithType(ErrorType.Validation);

    public static Error InvalidApiKey(string hosterCode) =>
        new Error(
            code: "Hoster.Credentials.InvalidApiKey",
            message: "Invalid API key.")
        .WithDetail($"The API key provided for Hoster '{hosterCode}' is invalid.")
        .WithType(ErrorType.Unauthorized);
}
