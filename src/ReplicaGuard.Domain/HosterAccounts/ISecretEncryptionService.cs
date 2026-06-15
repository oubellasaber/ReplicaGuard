namespace ReplicaGuard.Core.HosterAccounts;

public interface ISecretEncryptionService
{
    byte[] Encrypt(string plaintext);
    string Decrypt(byte[] ciphertext);
}
