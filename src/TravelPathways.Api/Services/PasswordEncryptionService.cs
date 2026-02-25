using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;

namespace TravelPathways.Api.Services;

public sealed class PasswordEncryptionService : IPasswordEncryption
{
    private const int KeySizeBytes = 32;
    private const int NonceSizeBytes = 12;
    private const int TagSizeBytes = 16;
    private readonly byte[] _key;

    public PasswordEncryptionService(IConfiguration configuration)
    {
        var keyB64 = configuration["Encryption:PasswordKey"];
        if (!string.IsNullOrWhiteSpace(keyB64))
        {
            try
            {
                _key = Convert.FromBase64String(keyB64.Trim());
                if (_key.Length != KeySizeBytes)
                    _key = DeriveKey(keyB64);
            }
            catch
            {
                _key = DeriveKey(keyB64 ?? "default");
            }
        }
        else
        {
            _key = DeriveKey(configuration["SuperAdmin:Password"] ?? "TravelPathwaysDefaultEncryptionKey");
        }
    }

    private static byte[] DeriveKey(string seed)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(seed);
        return SHA256.HashData(bytes);
    }

    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return string.Empty;

        var plainBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
        var nonce = RandomNumberGenerator.GetBytes(NonceSizeBytes);
        var tag = new byte[TagSizeBytes];
        var cipher = new byte[plainBytes.Length];

        using var aes = new AesGcm(_key, TagSizeBytes);
        aes.Encrypt(nonce, plainBytes, cipher, tag);

        var combined = new byte[NonceSizeBytes + tag.Length + cipher.Length];
        Buffer.BlockCopy(nonce, 0, combined, 0, NonceSizeBytes);
        Buffer.BlockCopy(tag, 0, combined, NonceSizeBytes, tag.Length);
        Buffer.BlockCopy(cipher, 0, combined, NonceSizeBytes + tag.Length, cipher.Length);
        return Convert.ToBase64String(combined);
    }

    public string? Decrypt(string? encrypted)
    {
        if (string.IsNullOrWhiteSpace(encrypted)) return null;
        try
        {
            var combined = Convert.FromBase64String(encrypted);
            if (combined.Length < NonceSizeBytes + TagSizeBytes) return null;

            var nonce = new byte[NonceSizeBytes];
            var tag = new byte[TagSizeBytes];
            var cipher = new byte[combined.Length - NonceSizeBytes - TagSizeBytes];
            Buffer.BlockCopy(combined, 0, nonce, 0, NonceSizeBytes);
            Buffer.BlockCopy(combined, NonceSizeBytes, tag, 0, TagSizeBytes);
            Buffer.BlockCopy(combined, NonceSizeBytes + TagSizeBytes, cipher, 0, cipher.Length);

            using var aes = new AesGcm(_key, TagSizeBytes);
            var plain = new byte[cipher.Length];
            aes.Decrypt(nonce, cipher, tag, plain);
            return System.Text.Encoding.UTF8.GetString(plain);
        }
        catch
        {
            return null;
        }
    }
}
