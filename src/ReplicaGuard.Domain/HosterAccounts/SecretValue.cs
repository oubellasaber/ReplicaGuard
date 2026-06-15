namespace ReplicaGuard.Core.HosterAccounts;

public sealed class SecretValue
{
    public byte[] CipherBytes { get; }

    public SecretValue(byte[] cipherBytes)
    {
        CipherBytes = cipherBytes ?? throw new ArgumentNullException(nameof(cipherBytes));
    }

    public static SecretValue CreateFromPlaintext(string plaintext, ISecretEncryptionService encryptionService)
    {
        if (string.IsNullOrWhiteSpace(plaintext))
            throw new ArgumentException("Plaintext cannot be null or whitespace.", nameof(plaintext));
        var cipherBytes = encryptionService.Encrypt(plaintext);
        return new SecretValue(cipherBytes);
    }

    public string Reveal(ISecretEncryptionService encryptionService)
    {
        return encryptionService.Decrypt(CipherBytes);
    }
}
