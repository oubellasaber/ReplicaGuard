using ReplicaGuard.Core.Abstractions;
using ReplicaGuard.Core.HosterAccounts;

namespace ReplicaGuard.Core.Hosters;

public interface IHosterDefinition
{
    HosterCode HosterId { get; }

    PrimaryIdentityRequirement PrimaryIdentities { get; }
    IReadOnlyList<CapabilityRequirement> CapabilityRequirements { get; }
    IReadOnlyList<IdentityGroup> IdentityGroups { get; }

    CapabilityRequirement? GetRequirement(CapabilityCode capability);
    IdentityGroup? GroupFor(IdentityType type);

    Result ValidatePrimaryCredentials(HosterAccount account);
    Result ValidateCapability(HosterAccount account, CapabilityCode capability);
}

public abstract class HosterDefinitionBase : IHosterDefinition
{
    public abstract HosterCode HosterId { get; }
    public abstract PrimaryIdentityRequirement PrimaryIdentities { get; }
    public abstract IReadOnlyList<CapabilityRequirement> CapabilityRequirements { get; }
    public abstract IReadOnlyList<IdentityGroup> IdentityGroups { get; }

    public CapabilityRequirement? GetRequirement(CapabilityCode capability)
        => CapabilityRequirements.SingleOrDefault(r => r.Capability == capability);

    public IdentityGroup? GroupFor(IdentityType type)
        => IdentityGroups.FirstOrDefault(g => g.GroupedIdentites.Contains(type));

    public Result ValidatePrimaryCredentials(HosterAccount account)
    {
        if (account.HosterId != HosterId)
            return Result.Failure(
                HosterErrors.AccountDoesNotBelongToHoster(account.HosterId, account.Id));

        if (!PrimaryIdentities.IsSatisfiedBy(account.Identities))
            return Result.Failure(
                HosterErrors.PrimaryIdentitiesNotSatisfied(account.HosterId));

        return Result.Success();
    }

    public Result ValidateCapability(HosterAccount account, CapabilityCode capability)
    {
        if (account.HosterId != HosterId)
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
}


public sealed class Pixeldrain : HosterDefinitionBase
{
    public override HosterCode HosterId => HosterCode.Pixeldrain;

    public override PrimaryIdentityRequirement PrimaryIdentities { get; }
    public override IReadOnlyList<CapabilityRequirement> CapabilityRequirements { get; }
    public override IReadOnlyList<IdentityGroup> IdentityGroups { get; }

    public Pixeldrain()
    {
        var emailUsernameGroup = new IdentityGroup(
            HosterId,
            new[] { IdentityType.Email, IdentityType.Username }
        );

        var apiGroup = new IdentityGroup(
            HosterId,
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
}

public sealed class SendCm : HosterDefinitionBase
{
    public override HosterCode HosterId => HosterCode.SendCm;
    public override PrimaryIdentityRequirement PrimaryIdentities { get; }
    public override IReadOnlyList<CapabilityRequirement> CapabilityRequirements { get; }
    public override IReadOnlyList<IdentityGroup> IdentityGroups { get; }

    public SendCm()
    {
        var apiGroup = new IdentityGroup(
            HosterId,
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
}

public static class HosterDefinitions
{
    public static readonly IReadOnlyList<IHosterDefinition> All = new IHosterDefinition[]
    {
        new Pixeldrain(),
        new SendCm()
    };
}
