using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.Punchout.Core.Security;

/// <summary>
/// The token is a 32-byte random value, so a plain SHA-256 without salt is enough.
/// </summary>
public static class SessionTokenHasher
{
    public static string Hash(string sessionToken)
    {
        if (sessionToken.IsNullOrEmpty())
        {
            return null;
        }

        return Base64Url.EncodeToString(SHA256.HashData(Encoding.UTF8.GetBytes(sessionToken)));
    }
}
