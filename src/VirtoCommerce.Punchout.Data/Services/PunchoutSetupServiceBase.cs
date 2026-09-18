using System;
using System.Buffers.Text;
using System.Security.Cryptography;
using System.Threading.Tasks;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Punchout.Core.Models;
using VirtoCommerce.Punchout.Core.Services;
using VirtoCommerce.StoreModule.Core.Services;

namespace VirtoCommerce.Punchout.Data.Services;

/// <summary>
/// The parts of a punchout setup that do not depend on how the caller is authenticated:
/// resolving the storefront of a store, minting the session token and building the start page URL.
/// </summary>
public abstract class PunchoutSetupServiceBase(IStoreService storeService) : IPunchoutSetupService
{
    protected const string StartPagePath = "punchout";

    /// <summary>
    /// Number of random bytes behind a session token. 32 bytes is 256 bits of entropy, which is what makes
    /// the start page URL safe to use as the only credential the buyer's browser presents.
    /// </summary>
    protected const int SessionTokenByteCount = 32;

    /// <summary>
    /// How long a start page URL stays usable. It only has to cover the redirect of the buyer's browser
    /// right after the setup response, so it is deliberately short.
    /// </summary>
    protected virtual TimeSpan SessionLifetime => TimeSpan.FromMinutes(15);

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

        // The buyer's browser is redirected to the start page, so prefer the HTTPS URL when the store has one.
        return string.IsNullOrEmpty(store.SecureUrl) ? store.Url : store.SecureUrl;
    }

    /// <summary>
    /// Creates the token that identifies the session in the start page URL. It is the only thing the
    /// buyer's browser presents when it arrives, so it comes from a cryptographic RNG and is encoded
    /// base64url to stay safe in a URL path.
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
