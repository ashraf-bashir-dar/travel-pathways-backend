namespace TravelPathways.Api.Services;

/// <summary>
/// Encrypts/decrypts passwords for reversible storage so admins can view user passwords.
/// </summary>
public interface IPasswordEncryption
{
    string Encrypt(string plainText);
    string? Decrypt(string? encrypted);
}
