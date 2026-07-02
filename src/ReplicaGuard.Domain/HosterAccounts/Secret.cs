using ReplicaGuard.Domain.Abstractions;

namespace ReplicaGuard.Domain.HosterAccounts;

public sealed class Secret : Entity<Guid>
{
    public SecretType Type { get; }
    public SecretValue Value { get; private set; } = null!;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private Secret() { }

    internal Secret(SecretType type, SecretValue value) : base(Guid.NewGuid())
    {
        Type = type;
        Value = value;
        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = CreatedAtUtc;
    }

    public static Secret CreateNew(SecretType type, SecretValue secret)
    {
        return new Secret(type, secret);
    }

    internal void Update(SecretValue encryptedValue)
    {
        Value = encryptedValue;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
