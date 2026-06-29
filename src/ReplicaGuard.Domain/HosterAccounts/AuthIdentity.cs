using ReplicaGuard.Domain.Abstractions;

namespace ReplicaGuard.Domain.HosterAccounts;

public sealed class AuthIdentity : Entity<Guid>
{
    public IdentityType Type { get; }
    // is active
    public string? Value { get; }

    // The identity does NOT own secrets.
    // It references a SecretSet that contains the actual secrets.
    // This allows multiple identities to share the same secrets if needed.
    public SecretSet SecretSet { get; } = null!;
    public IdentityVerificationStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; }
    public DateTime UpdatedAtUtc { get; private set; }

    private AuthIdentity() { }

    internal AuthIdentity(
        IdentityType type,
        string? value,
        SecretSet secretSet) : base(Guid.NewGuid())
    {
        Type = type;
        Value = value;
        SecretSet = secretSet ?? throw new ArgumentNullException(nameof(secretSet));
        Status = IdentityVerificationStatus.Pending;
        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = CreatedAtUtc;
        RaiseDomainEvent(new IdentityCreatedDomainEvent(Id));
    }

    internal static AuthIdentity CreateNew(
        IdentityType type,
        string? value,
        SecretSet secretSet)
    {
        // 1. Enforce value requirement
        if (type.RequiresValue() && string.IsNullOrWhiteSpace(value))
            throw new Exception(
                $"Identity type '{type}' requires a non-empty value.");

        if (!type.RequiresValue() && value is not null)
            throw new Exception(
                $"Identity type '{type}' must not have a value.");

        return new AuthIdentity(
            type,
            value,
            secretSet
        );
    }

    public string RevealSecret(
        SecretType secretType,
        ISecretEncryptionService encryptionService)
    {
        var secret = SecretSet.GetSecret(secretType);
        return secret.Value.Reveal(encryptionService);
    }

    public void MarkAsVerifying()
    {
        Status = IdentityVerificationStatus.Verifying;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void MarkAsVerified()
    {
        Status = IdentityVerificationStatus.Verified;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void MarkAsRejected()
    {
        Status = IdentityVerificationStatus.Rejected;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}

public enum IdentityVerificationStatus
{
    Pending = 1,
    Verifying = 2,
    Verified = 3,
    Rejected = 4
}
