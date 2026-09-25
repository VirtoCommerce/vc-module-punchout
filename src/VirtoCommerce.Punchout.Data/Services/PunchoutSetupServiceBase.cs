using System.Buffers.Text;
using System.Security.Cryptography;
using System.Threading.Tasks;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Punchout.Core.Models;
using VirtoCommerce.Punchout.Core.Services;
using VirtoCommerce.StoreModule.Core.Services;

namespace VirtoCommerce.Punchout.Data.Services;

public abstract class PunchoutSetupServiceBase(IStoreService storeService) : IPunchoutSetupService
{
    protected const string StartPagePath = "punchout";

    protected const int SessionTokenByteCount = 32;

    public abstract Task<PunchoutSetupResult> ProcessAsync(PunchoutSetupContext punchoutSetupContext);

    protected virtual async Task<string> GetStorefrontUrlAsync(string storeId)
    {
        if (string.IsNullOrEmpty(storeId))
        {
            return null;
        }

        var store = await storeService.GetByIdAsync(storeId);

        if (store is null)
        {
            return null;
        }

        return string.IsNullOrEmpty(store.SecureUrl) ? store.Url : store.SecureUrl;
    }

    /// <summary>
    /// Creates the token that identifies the session in the start page URL. It is the only thing the
    /// buyer's browser presents when it arrives, so it comes from an RNG (32 bytes) and is encoded
    /// base64url to stay safe in an URL path.
    /// </summary>
    protected virtual string CreateSessionToken()
    {
        return Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(SessionTokenByteCount));
    }

    protected virtual string BuildStartPage(string storefrontUrl, string sessionToken)
    {
        return $"{storefrontUrl.TrimEnd('/')}/{StartPagePath}/{sessionToken}";
    }
}
