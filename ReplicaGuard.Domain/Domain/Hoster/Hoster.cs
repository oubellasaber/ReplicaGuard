using ReplicaGuard.Core.Abstractions;
using ReplicaGuard.Core.Domain.Credentials;

namespace ReplicaGuard.Core.Domain.Hoster;

public class Hoster : Entity<Guid>
{
    private readonly List<FeatureRequirement> _requirements = new();

    public string Code { get; private set; } = default!;
    public string DisplayName { get; private set; } = default!;
    public Credentials PrimaryCredentials { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }

    public IReadOnlyList<FeatureRequirement> Requirements => _requirements.AsReadOnly();

    // EF Core
    private Hoster()
        : base(Guid.NewGuid()) { }

    public static Result<Hoster> Create(
        string code,
        string displayName,
        Credentials primaryCredentials)
    {
        if (string.IsNullOrWhiteSpace(code))
            return Result.Failure<Hoster>(HosterErrors.CodeEmpty);

        if (string.IsNullOrWhiteSpace(displayName))
            return Result.Failure<Hoster>(HosterErrors.DisplayNameEmpty);

        if (primaryCredentials == Credentials.None)
            return Result.Failure<Hoster>(HosterErrors.InvalidAuthMethod);

        var hoster = new Hoster
        {
            Id = Guid.NewGuid(),
            Code = code.ToUpperInvariant().Trim(),
            DisplayName = displayName.Trim(),
            PrimaryCredentials = primaryCredentials,
            CreatedAtUtc = DateTime.UtcNow
        };

        return hoster;
    }

    public Result AddFeatureRequirement(CapabilityCode feature, Credentials requiredCredentials)
    {
        if (_requirements.Any(r => r.Feature == feature))
            return Result.Failure<Hoster>(HosterErrors.FeatureAlreadyExists(feature));

        _requirements.Add(FeatureRequirement.Create(feature, requiredCredentials));
        return Result.Success();
    }

    /// <summary>
    /// Creates credentials with the provided values. Only credentials matching the hoster's
    /// primary credential types are accepted; others are ignored.
    /// </summary>
    public Result<HosterCredentials> CreateCredentials(
        Guid userId,
        string? apiKey = null,
        string? email = null,
        string? username = null,
        string? password = null)
    {
        return HosterCredentials.Create(userId, Id, PrimaryCredentials, apiKey, email, username, password);
    }

    /// <summary>
    /// Updates credentials in bulk. Only updates credentials matching the hoster's
    /// primary credential types. Raises a single domain event.
    /// </summary>
    public Result UpdateCredentials(
        HosterCredentials credentials,
        string? apiKey = null,
        string? email = null,
        string? username = null,
        string? password = null)
    {
        ValidateCredentialsBelongToHoster(credentials);
        return credentials.Update(apiKey, email, username, password);
    }

    private void ValidateCredentialsBelongToHoster(HosterCredentials credentials)
    {
        if (credentials.HosterId != Id)
            throw new InvalidOperationException("Credentials do not belong to this hoster.");

        if ((credentials.PrimaryCredentials & PrimaryCredentials) == 0)
            throw new InvalidOperationException(
                $"Credentials primary method {credentials.PrimaryCredentials} does not match hoster requirement {PrimaryCredentials}.");
    }
}
