using System.Net;
using ReplicaGuard.Core.Abstractions;
using ReplicaGuard.Core.Domain.Common;

namespace ReplicaGuard.Core.Domain.Hoster;

public class HosterErrors
{
    public static Error CodeEmpty =>
        new Error("Hoster.CodeEmpty", "Hoster code cannot be empty.")
        .WithType(ErrorType.Validation);

    public static Error DisplayNameEmpty =>
        new Error("Hoster.DisplayNameEmpty", "Hoster display name cannot be empty.")
        .WithType(ErrorType.Validation);

    public static Error InvalidAuthMethod =>
        new Error("Hoster.InvalidAuthMethod", "Hoster must have a primary authentication method.")
        .WithType(ErrorType.Validation);

    public static Error NotFound(Guid id) =>
        CommonErrors.NotFound(nameof(Hoster), id);

    public static Error FeatureAlreadyExists(CapabilityCode feature) =>
        new Error("Hoster.FeatureExists", $"Feature '{feature}' already exists for this hoster.")
        .WithType(ErrorType.Conflict);
}
