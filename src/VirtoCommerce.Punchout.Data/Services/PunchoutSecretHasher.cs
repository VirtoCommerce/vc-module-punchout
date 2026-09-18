using System;
using System.Globalization;
using System.Security.Cryptography;
using VirtoCommerce.Punchout.Core.Services;

namespace VirtoCommerce.Punchout.Data.Services;

/// <summary>
/// Hashes punchout shared secrets with PBKDF2 (HMAC-SHA256).
/// The hash is stored as "{version}.{iterations}.{base64 salt}.{base64 hash}" so that the work factor
/// can be raised later without invalidating the secrets hashed with the current one.
/// </summary>
public class PunchoutSecretHasher : IPunchoutSecretHasher
{
    private const string CurrentVersion = "v1";
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int CurrentIterations = 100_000;

    private static readonly HashAlgorithmName _algorithm = HashAlgorithmName.SHA256;

    public string HashSecret(string secret)
    {
        ArgumentException.ThrowIfNullOrEmpty(secret);

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(secret, salt, CurrentIterations, _algorithm, HashSize);

        return string.Join('.', CurrentVersion, CurrentIterations, Convert.ToBase64String(salt), Convert.ToBase64String(hash));
    }

    public bool VerifySecret(string secret, string hash)
    {
        if (string.IsNullOrEmpty(secret) || string.IsNullOrEmpty(hash))
        {
            return false;
        }

        var parts = hash.Split('.');

        if (parts.Length != 4 ||
            parts[0] != CurrentVersion ||
            !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var iterations) ||
            iterations <= 0)
        {
            return false;
        }

        var salt = FromBase64OrNull(parts[2]);
        var expectedHash = FromBase64OrNull(parts[3]);

        if (salt is null || expectedHash is null || expectedHash.Length == 0)
        {
            return false;
        }

        var actualHash = Rfc2898DeriveBytes.Pbkdf2(secret, salt, iterations, _algorithm, expectedHash.Length);

        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }

    private static byte[] FromBase64OrNull(string value)
    {
        try
        {
            return Convert.FromBase64String(value);
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
