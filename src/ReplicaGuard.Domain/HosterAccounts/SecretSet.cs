using ReplicaGuard.Core.Abstractions;

namespace ReplicaGuard.Core.HosterAccounts;

// A cohesive authentication bundle required by the hoster.
// Think of password and otp secret key as a secret set.
// They are different secrets but they belong together and usually required together for authentication.
// They are stored and managed together as a set of secrets.
public sealed class SecretSet : Entity<Guid>
{
    private readonly List<Secret> _secrets = new();
    public IReadOnlyList<Secret> Secrets => _secrets;
    // validation to ensure that the secrets in the set are consistent with each other and with the hoster's requirements can be added here.

    private SecretSet() { }

    private SecretSet(IEnumerable<Secret> secrets) : base(Guid.NewGuid())
    {
        _secrets = secrets.ToList();
    }

    public static SecretSet Create(IEnumerable<Secret> secrets)
    {
        return new SecretSet(secrets);
    }

    public Secret GetSecret(SecretType type)
        => _secrets.Single(s => s.Type == type);

    internal void UpdateSecret(SecretType type, SecretValue encryptedValue)
    {
        var secret = GetSecret(type);
        secret.Update(encryptedValue);
    }

    internal void UpdateSecrets(Dictionary<SecretType, SecretValue> newValues)
    {
        foreach (var (type, encryptedValue) in newValues)
        {
            UpdateSecret(type, encryptedValue);
        }
    }
}
