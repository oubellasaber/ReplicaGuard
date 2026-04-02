using ReplicaGuard.Core.Abstractions;

namespace ReplicaGuard.Application.Assets;

public static class AssetErrors
{
    public static Error MissingCredentials(Guid hosterId) =>
        new("Asset.MissingCredentials",
            $"No credentials found for hoster '{hosterId}'. Add credentials before creating assets.",
            ErrorType.Failure);

    public static Error CredentialsNotSynced(Guid hosterId) =>
        new("Asset.CredentialsNotSynced",
            $"Credentials for hoster '{hosterId}' are not yet validated. Please wait for validation to complete.",
            ErrorType.Failure);
}
