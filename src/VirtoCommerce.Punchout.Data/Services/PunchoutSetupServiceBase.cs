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
    /// Creates the token that identifies the session in the start page URL. 
    /// </summary>
    protected virtual string CreateSessionToken()
    {
        // RNG (32 bytes) and base64url to stay safe in an URL path
        return Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(SessionTokenByteCount));
    }

    /// <summary>
    /// The start page without the session token.
    /// </summary>
    protected virtual string BuildStartPage(string storefrontUrl)
    {
        return $"{storefrontUrl.TrimEnd('/')}/{StartPagePath}";
    }

    /// <summary>
    /// The start page returned to the buyer, with the session token.
    /// </summary>
    protected virtual string BuildStartPageUrl(string startPage, string sessionToken)
    {
        return $"{startPage}/{sessionToken}";
    }
}
