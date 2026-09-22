namespace VirtoCommerce.Punchout.Core.Services;

public interface IPunchoutSecretHasher
{
    string HashSecret(string secret);

    bool VerifySecret(string secret, string hash);
}
