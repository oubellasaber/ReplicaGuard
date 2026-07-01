namespace ReplicaGuard.Domain.HosterAccounts;

public sealed class Secret
{
    public Guid Id { get; }
    public SecretType Type { get; }
    public SecretValue Value { get; private set; } = null!;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private Secret() { }

    internal Secret(Guid id, SecretType type, SecretValue value)
    {
        Id = id;
        Type = type;
        Value = value;
        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = CreatedAtUtc;
    }

    public static Secret CreateNew(SecretType type, SecretValue secret)
    {
        return new Secret(Guid.NewGuid(), type, secret);
    }

    internal void Update(SecretValue encryptedValue)
    {
        Value = encryptedValue;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
