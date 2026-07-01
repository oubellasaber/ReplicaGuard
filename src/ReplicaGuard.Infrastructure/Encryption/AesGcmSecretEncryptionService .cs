using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using ReplicaGuard.Domain.HosterAccounts;

namespace ReplicaGuard.Infrastructure.Encryption;

public sealed class AesGcmSecretEncryptionService : ISecretEncryptionService
{
    private readonly byte[] _key;

    // base64Key must be a 32‑byte (256‑bit) key encoded as Base64
    public AesGcmSecretEncryptionService(IOptions<EncryptionOptions> options)
    {
        var base64Key = options.Value.Base64Key;
        _key = Convert.FromBase64String(base64Key);

        if (_key.Length != 32)
            throw new ArgumentException("Key must be 256 bits (32 bytes).", nameof(base64Key));
    }

    public byte[] Encrypt(string plaintext)
    {
        if (plaintext is null)
            throw new ArgumentNullException(nameof(plaintext));

        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);

        // GCM recommended nonce size = 12 bytes
        var nonce = RandomNumberGenerator.GetBytes(12);
        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[16]; // 128‑bit auth tag

        using var aes = new AesGcm(_key, tag.Length);
        aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);

        // Final payload layout: [nonce][tag][ciphertext]
        var result = new byte[nonce.Length + tag.Length + ciphertext.Length];

        Buffer.BlockCopy(nonce, 0, result, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, result, nonce.Length, tag.Length);
        Buffer.BlockCopy(ciphertext, 0, result, nonce.Length + tag.Length, ciphertext.Length);

        return result;
    }

    public string Decrypt(byte[] ciphertext)
    {
        if (ciphertext is null || ciphertext.Length < 12 + 16)
            throw new ArgumentException("Invalid encrypted payload.", nameof(ciphertext));

        var nonce = new byte[12];
        var tag = new byte[16];
        var actualCiphertext = new byte[ciphertext.Length - nonce.Length - tag.Length];

        Buffer.BlockCopy(ciphertext, 0, nonce, 0, nonce.Length);
        Buffer.BlockCopy(ciphertext, nonce.Length, tag, 0, tag.Length);
        Buffer.BlockCopy(ciphertext, nonce.Length + tag.Length, actualCiphertext, 0, actualCiphertext.Length);

        var plaintextBytes = new byte[actualCiphertext.Length];

        using var aes = new AesGcm(_key, tag.Length);
        aes.Decrypt(nonce, actualCiphertext, tag, plaintextBytes);

        return Encoding.UTF8.GetString(plaintextBytes);
    }
}
