namespace VirtoCommerce.Punchout.Core.Services;

public interface IPunchoutSecretHasher
{
    /// <summary>
    /// Produces a salted one-way hash of the shared secret, suitable for storing in the database.
    /// </summary>
    string HashSecret(string secret);

    /// <summary>
    /// Verifies a plain shared secret against a hash produced by <see cref="HashSecret"/>.
    /// </summary>
    bool VerifySecret(string secret, string hash);
}
