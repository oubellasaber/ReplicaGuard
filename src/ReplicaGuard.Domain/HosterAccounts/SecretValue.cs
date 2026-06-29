namespace ReplicaGuard.Domain.HosterAccounts;

public sealed class SecretValue
{
    public byte[] CipherBytes { get; }

    public SecretValue(byte[] cipherBytes)
    {
        CipherBytes = cipherBytes ?? throw new ArgumentNullException(nameof(cipherBytes));
    }

    public static SecretValue CreateFromPlaintext(string plaintextPass, ISecretEncryptionService encryptionService)
    {
        if (string.IsNullOrWhiteSpace(plaintextPass))
            throw new ArgumentException("Plaintext password cannot be null or whitespace.", nameof(plaintextPass));
        var cipherBytes = encryptionService.Encrypt(plaintextPass);
        return new SecretValue(cipherBytes);
    }

    public string Reveal(ISecretEncryptionService encryptionService)
    {
        return encryptionService.Decrypt(CipherBytes);
    }
}
