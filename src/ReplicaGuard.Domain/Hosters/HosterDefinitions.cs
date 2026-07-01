using ReplicaGuard.Domain.Abstractions;
using ReplicaGuard.Domain.HosterAccounts;

namespace ReplicaGuard.Domain.Hosters;

public interface IHosterDefinition
{
    HosterCode Code { get; }

    PrimaryIdentityRequirement PrimaryIdentities { get; }
    IReadOnlyList<CapabilityRequirement> CapabilityRequirements { get; }
    IReadOnlyList<IdentityGroup> IdentityGroups { get; }

    CapabilityRequirement? GetRequirement(CapabilityCode capability);
    IdentityGroup? GroupFor(IdentityType type);

    Result ValidatePrimaryCredentials(HosterAccount account);
    Result ValidateCapability(HosterAccount account, CapabilityCode capability);
    Result<string> ExtractFileCode(Uri url);
}

public abstract class HosterDefinitionBase : IHosterDefinition
{
    public abstract HosterCode Code { get; }
    public abstract PrimaryIdentityRequirement PrimaryIdentities { get; }
    public abstract IReadOnlyList<CapabilityRequirement> CapabilityRequirements { get; }
    public abstract IReadOnlyList<IdentityGroup> IdentityGroups { get; }

    public CapabilityRequirement? GetRequirement(CapabilityCode capability)
        => CapabilityRequirements.SingleOrDefault(r => r.Capability == capability);

    public IdentityGroup? GroupFor(IdentityType type)
        => IdentityGroups.FirstOrDefault(g => g.GroupedIdentites.Contains(type));

    public Result ValidatePrimaryCredentials(HosterAccount account)
    {
        if (account.HosterId != Code)
            return Result.Failure(
                HosterErrors.AccountDoesNotBelongToHoster(account.HosterId, account.Id));

        if (!PrimaryIdentities.IsSatisfiedBy(account.Identities))
            return Result.Failure(
                HosterErrors.PrimaryIdentitiesNotSatisfied(account.HosterId));

        return Result.Success();
    }

    public Result ValidateCapability(HosterAccount account, CapabilityCode capability)
    {
        if (account.HosterId != Code)
            return Result.Failure(
                HosterErrors.AccountDoesNotBelongToHoster(account.HosterId, account.Id));

        var requirement = GetRequirement(capability);
        if (requirement == null)
            return Result.Failure(
                HosterErrors.CapabilityNotSupported(account.HosterId, capability));

        if (!requirement.IsSatisfiedBy(account.Identities))
            return Result.Failure(
                HosterErrors.CapabilityRequirementsNotSatisfied(account.HosterId, capability));
        return Result.Success();
    }

    public abstract Result<string> ExtractFileCode(Uri url);
}


public sealed class Pixeldrain : HosterDefinitionBase
{
    public override HosterCode Code => HosterCode.Pixeldrain;

    public override PrimaryIdentityRequirement PrimaryIdentities { get; }
    public override IReadOnlyList<CapabilityRequirement> CapabilityRequirements { get; }
    public override IReadOnlyList<IdentityGroup> IdentityGroups { get; }

    public Pixeldrain()
    {
        var emailUsernameGroup = new IdentityGroup(
            Code,
            new[] { IdentityType.Email, IdentityType.Username }
        );

        var apiGroup = new IdentityGroup(
            Code,
            new[] { IdentityType.ApiKey }
        );

        IdentityGroups = new[] { emailUsernameGroup, apiGroup };

        PrimaryIdentities = new PrimaryIdentityRequirement(new[]
        {
            new RequirementPath(new[] { IdentityType.ApiKey }),
            //new RequirementPath(new[] { IdentityType.Email }),
            //new RequirementPath(new[] { IdentityType.Username })
        });

        CapabilityRequirements = new List<CapabilityRequirement>
        {
            new CapabilityRequirement(
                CapabilityCode.LocalFileUpload,
                new[]
                {
                    new RequirementPath(new[] { IdentityType.ApiKey })
                })
        };
    }

    public override Result<string> ExtractFileCode(Uri url)
    {
        ArgumentNullException.ThrowIfNull(url, nameof(url));

        var host = url.Host.ToLowerInvariant();

        if (host != "pixeldrain.com" && host != "pixeldra.in")
            return Result.Failure<string>(
                HosterErrors.UnsupportedHosterDomain(host));

        var segments = url.AbsolutePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length < 2)
            return Result.Failure<string>(
                HosterErrors.MissingFileCode(url));

        if (segments.Length >= 2 &&
            segments[0].Equals("u", StringComparison.OrdinalIgnoreCase))
        {
            return Result.Success(segments[1]);
        }

        return Result.Failure<string>(
                HosterErrors.MissingFileCode(url));
    }
}

public sealed class SendCm : HosterDefinitionBase
{
    public override HosterCode Code => HosterCode.SendCm;
    public override PrimaryIdentityRequirement PrimaryIdentities { get; }
    public override IReadOnlyList<CapabilityRequirement> CapabilityRequirements { get; }
    public override IReadOnlyList<IdentityGroup> IdentityGroups { get; }

    public SendCm()
    {
        var apiGroup = new IdentityGroup(
            Code,
            new[] { IdentityType.ApiKey }
        );

        IdentityGroups = new[] { apiGroup };

        PrimaryIdentities = new PrimaryIdentityRequirement(new[]
        {
            new RequirementPath(new[] { IdentityType.ApiKey })
        });

        CapabilityRequirements = new List<CapabilityRequirement>
        {
            new CapabilityRequirement(
                CapabilityCode.LocalFileUpload,
                new[]
                {
                    new RequirementPath(new[] { IdentityType.ApiKey })
                }),
            new CapabilityRequirement(
                CapabilityCode.RemoteFileUpload,
                new[]
                {
                    new RequirementPath(new[] { IdentityType.ApiKey })
                })
        };
    }

    public override Result<string> ExtractFileCode(Uri url)
    {
        ArgumentNullException.ThrowIfNull(url, nameof(url));

        // Ensure it's a supported host
        var host = url.Host.ToLowerInvariant();
        if (host != "send.cm" && host != "send.now")
            return Result.Failure<string>(HosterErrors.UnsupportedHosterDomain(host));

        // Get last segment of the path
        var segments = url.AbsolutePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length == 0)
            return Result.Failure<string>(HosterErrors.MissingFileCode(url));

        var code = segments[^1];

        return code;
    }
}

public static class HosterDefinitions
{
    public static readonly IReadOnlyList<IHosterDefinition> All = new IHosterDefinition[]
    {
        new Pixeldrain(),
        new SendCm()
    };
}
