using ReplicaGuard.Domain.HosterAccounts;

namespace ReplicaGuard.Domain.Tests;

internal sealed class FakeEncryptionService : ISecretEncryptionService
{
    public byte[] Encrypt(string plaintext) => System.Text.Encoding.UTF8.GetBytes(plaintext);

    public string Decrypt(byte[] ciphertext) => System.Text.Encoding.UTF8.GetString(ciphertext);
}
