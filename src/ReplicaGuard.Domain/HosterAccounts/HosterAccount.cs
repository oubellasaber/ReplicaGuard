using ReplicaGuard.Domain.Abstractions;
using ReplicaGuard.Domain.Hosters;

namespace ReplicaGuard.Domain.HosterAccounts;

public sealed class HosterAccount : Entity<Guid>
{
    public Hoster Hoster { get; private set; } = null!;
    public Guid HosterId { get; private set; }
    public HosterCode HosterCode => Hoster.Code;

    public Guid UserId { get; }
    public string Alias { get; } = null!;
    public string? Description { get; }

    // InternalId is simply the most stable identifier we can extract from the hoster.
    // It doesn’t matter what it is — username, numeric ID, UUID, opaque token — as long as it uniquely identifies the same user across time.
    //public string? InternalId { get; private set; }

    public DateTime CreatedAtUtc { get; }
    public DateTime UpdatedAtUtc { get; }

    private readonly List<AuthIdentity> _identities = new List<AuthIdentity>();
    public IReadOnlyList<AuthIdentity> Identities => _identities.AsReadOnly();

    internal HosterAccount() { }

    internal HosterAccount(
        Guid hosterId,
        Guid userId,
        string alias,
        string? description)
        : base(Guid.NewGuid())
    {
        HosterId = hosterId;
        UserId = userId;
        Alias = alias;
        Description = description;
        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = CreatedAtUtc;
    }

    public static Result<HosterAccount> Create(
        IHosterDefinition hosterDefinition,
        Hoster hoster,
        Guid userId,
        string alias,
        string? description,
        IEnumerable<IdentityPayload> initialIdentities,
        ISecretEncryptionService encryptionService)
    {
        if (initialIdentities == null || !initialIdentities.Any())
            return Result.Failure<HosterAccount>(
                HosterAccountErrors.PrimaryIdentitiesNotSatisfied(hosterDefinition.PrimaryIdentities, hoster.Code));

        var account = new HosterAccount(
            hoster.Id,
            userId,
            alias,
            description);

        foreach (var pending in initialIdentities)
        {
            account.AddIdentity(
                hosterDefinition,
                pending,
                encryptionService);
        }

        if (!hosterDefinition.PrimaryIdentities.IsSatisfiedBy(account.Identities))
            return Result.Failure<HosterAccount>(
                HosterAccountErrors.PrimaryIdentitiesNotSatisfied(hosterDefinition.PrimaryIdentities, hosterDefinition.Code));

        return Result.Success(account);
    }

    public AuthIdentity AddIdentity(
        IHosterDefinition hosterDefinition,
        IdentityPayload payload,
        ISecretEncryptionService encryption)
    {
        return payload switch
        {
            IdentityPayload.EmailPayload e =>
                AddIdentity(
                    hosterDefinition,
                    IdentityType.Email,
                    e.Email,
                    new Dictionary<SecretType, string>
                    {
                    { SecretType.Password, e.Password }
                    },
                    encryption),

            IdentityPayload.UsernamePayload u =>
                AddIdentity(
                    hosterDefinition,
                    IdentityType.Username,
                    u.Username,
                    new Dictionary<SecretType, string>
                    {
                    { SecretType.Password, u.Password }
                    },
                    encryption),

            IdentityPayload.ApiKeyPayload a =>
                AddIdentity(
                    hosterDefinition,
                    IdentityType.ApiKey,
                    null,
                    new Dictionary<SecretType, string>
                    {
                    { SecretType.ApiKeyPair, a.ApiKey }
                    },
                    encryption),

            _ => throw new InvalidOperationException("Unknown identity payload type")
        };

        // todo: fire identity added domain event, but we need to decide what info to include in the event first. We probably want to avoid including sensitive info like the identity value or secrets, but we might want to include the identity type and/or id for the new identity.
    }


    /// <summary>
    /// Adds a new identity to this hoster account.
    /// 
    /// Domain rules enforced:
    /// 1. The provided <see cref="IHosterDefinition"/> MUST match this account's <see cref="Hosters.HosterCode"/>.
    ///    This prevents mixing identity rules from the wrong hoster.
    /// 
    /// 2. The <see cref="IdentityType"/> MUST belong to exactly one <see cref="IdentityGroup"/>.
    ///    IdentityGroups define which identities share the same SecretSet.
    /// 
    /// 3. The provided secrets MUST match the group's RequiredSecrets exactly:
    ///       - No missing secrets
    ///       - No extra secrets
    ///       - No wrong secret types
    ///    This ensures the SecretSet is always valid for the group.
    /// 
    /// 4. SecretSet reuse:
    ///       - If another identity in the same group already exists,
    ///         its SecretSet is reused.
    ///       - Otherwise, a new SecretSet is created.
    ///    This guarantees grouped identities always share the same secrets.
    /// 
    /// 5. A new <see cref="AuthIdentity"/> is created referencing the resolved SecretSet.
    /// 
    /// This method guarantees the aggregate cannot enter an invalid state.
    /// </summary>
    private AuthIdentity AddIdentity(
        IHosterDefinition hosterDefinition,
        IdentityType type,
        string? value,
        Dictionary<SecretType, string> plaintextSecrets,
        ISecretEncryptionService encryptionService)
    {
        // 1. Determine which identity group this identity belongs to.
        //    Each identity type MUST belong to exactly one group.
        var group = hosterDefinition.GroupFor(type);

        if (group == null)
            throw new InvalidOperationException("No identity group found for the specified type.");

        // Convert plaintext => encrypted Secret objects
        var encryptedSecrets = plaintextSecrets
            .Select(kvp => Secret.CreateNew(kvp.Key, SecretValue.CreateFromPlaintext(kvp.Value, encryptionService)))
            .ToList();

        // 2. Resolve the correct SecretSet:
        //    - Reuse an existing one if another identity in the same group exists.
        //    - Otherwise create a new SecretSet.
        var secretSet = ResolveSecretSetFor(hosterDefinition, type, group, encryptedSecrets);
        // 4. Create the identity and attach it to the account.
        var identity = AuthIdentity.CreateNew(type, value, secretSet);
        _identities.Add(identity);

        return identity;
    }

    /// <summary>
    /// Resolves the SecretSet to use for the given identity.
    /// 
    /// Domain rules enforced:
    /// 
    /// 1. SecretSet reuse:
    ///    If any existing identity in the same IdentityGroup already exists,
    ///    its SecretSet MUST be reused.
    /// 
    ///    This ensures:
    ///    - Email + Username share the same Password
    ///    - ApiKey identities share the same ApiKey
    ///    - Group invariants remain consistent
    /// 
    /// 2. SecretSet creation:
    ///    If no identity in the group exists yet, a new SecretSet is created.
    /// 
    /// NOTE:
    /// HosterAccount does NOT store SecretSets directly.
    /// SecretSets are owned by AuthIdentity.
    /// This method discovers existing SecretSets by scanning identities.
    /// </summary>
    private SecretSet ResolveSecretSetFor(
        IHosterDefinition hoster,
        IdentityType type,
        IdentityGroup group,
        IEnumerable<Secret> secrets)
    {
        // Look for an existing identity in the same group.
        // If found, reuse its SecretSet.
        var existing = _identities
            .Where(i => hoster.GroupFor(i.Type) == group)
            .Select(i => i.SecretSet)
            .FirstOrDefault();

        if (existing != null)
            return existing;

        // No identity in this group yet then create a new SecretSet.
        return SecretSet.Create(secrets);
    }

    // TODO: allow update of identity value
    public void UpdateIdentity(
        IdentityType identityType,
        IdentityPayload payload,
        ISecretEncryptionService encryption)
    {
        var secrets = payload switch
        {
            IdentityPayload.EmailPayload e =>
            new Dictionary<SecretType, string>
            {
                { SecretType.Password, e.Password }
            },

            IdentityPayload.UsernamePayload u =>
            new Dictionary<SecretType, string>
            {
                { SecretType.Password, u.Password }
            },

            IdentityPayload.ApiKeyPayload a =>
            new Dictionary<SecretType, string>
            {
                { SecretType.ApiKeyPair, a.ApiKey }
            },

            _ => throw new InvalidOperationException("Unknown identity update payload type")
        };

        UpdateSecrets(identityType, secrets, encryption);
    }


    // This method also should not allow such invalid input, but we might want to validate this earlier at the API level to give better error messages.
    private void UpdateSecrets(
        IdentityType identityType,
        Dictionary<SecretType, string> plaintextSecrets,
        ISecretEncryptionService encryptionService)
    {
        var identity = _identities.Single(i => i.Type == identityType);

        // Convert plaintext → encrypted
        var encrypted = plaintextSecrets.ToDictionary(
            kv => kv.Key,
            kv => SecretValue.CreateFromPlaintext(kv.Value, encryptionService)
        );

        // Update the entire bundle atomically
        identity.SecretSet.UpdateSecrets(encrypted);

        //AddDomainEvent(new SecretBundleUpdated(Id, identityType));
    }


    // Validate primary credentials (OR-of-ANDs)
    public bool HasValidPrimaryIdentities(IHosterDefinition hoster)
    {
        if (hoster.Code != Hoster.Code)
            throw new InvalidOperationException("The provided hoster does not match the hoster account's hoster.");

        var requirement = hoster.PrimaryIdentities;
        return requirement.IsVerifiedSatisfiedBy(_identities);
    }

    // Check if the account can perform a specific capability by validating the associated requirement against the account's identities. (OR-of-ANDs)
    internal bool CanPerform(CapabilityRequirement requirement)
        => requirement.IsSatisfiedBy(_identities);

    public AuthIdentity GetAuthIdentity(IdentityType type)
    {
        var identity = _identities.SingleOrDefault(i => i.Type == type);
        if (identity == null)
            throw new InvalidOperationException($"Identity of type {type} not found in this account.");
        return identity;
    }

    public Result<string> GetApiKey(ISecretEncryptionService secretEncryptionService)
    {
        var apiKeyIdentity = _identities
            .FirstOrDefault(id => id.Type == IdentityType.ApiKey);

        if (apiKeyIdentity is null)
            return Result.Failure<string>(AuthIdentityErrors.IdentityMissing(Id, IdentityType.ApiKey));

        if (apiKeyIdentity.Status != IdentityVerificationStatus.Verified)
            return Result.Failure<string>(AuthIdentityErrors.IdentityNotVerified(Id, apiKeyIdentity.Id));

        var decryptedApiKey = apiKeyIdentity
            .RevealSecret(SecretType.ApiKeyPair, secretEncryptionService);

        return decryptedApiKey;
    }
}
