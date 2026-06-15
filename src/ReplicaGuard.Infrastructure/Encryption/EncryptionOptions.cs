namespace ReplicaGuard.Infrastructure.Encryption;

public sealed class EncryptionOptions
{
    public const string SectionName = "Encryption";
    public required string Base64Key { get; set; }
}
