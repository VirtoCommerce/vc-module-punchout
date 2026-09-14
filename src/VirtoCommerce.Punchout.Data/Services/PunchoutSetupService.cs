using System;
using System.Buffers.Text;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Punchout.Core;
using VirtoCommerce.Punchout.Core.Models;
using VirtoCommerce.Punchout.Core.Services;
using VirtoCommerce.StoreModule.Core.Services;

namespace VirtoCommerce.Punchout.Data.Services;

public class PunchoutSetupService(
    IPunchoutIntegrationSearchService integrationSearchService,
    IPunchoutSecretHasher secretHasher,
    IPunchoutSessionService sessionService,
    IStoreService storeService,
    ILogger<PunchoutSetupService> logger)
    : IPunchoutSetupService
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

    public virtual async Task<PunchoutSetupResult> ProcessAsync(PunchoutSetupContext punchoutSetupContext)
    {
        ArgumentNullException.ThrowIfNull(punchoutSetupContext);

        var integration = await FindIntegrationAsync(punchoutSetupContext.Sender);

        if (integration is null || !secretHasher.VerifySecret(punchoutSetupContext.SharedSecret, integration.SharedSecretHash))
        {
            // An unknown sender and a wrong secret give the same answer on purpose, so that the response
            // does not tell the caller which of the two it got wrong.
            logger.LogWarning("Punchout setup rejected for sender identity '{SenderIdentity}'.", punchoutSetupContext.Sender);

            return PunchoutSetupResult.Error(PunchoutSetupStatus.InvalidCredentials);
        }

        var storefrontUrl = await GetStorefrontUrlAsync(integration.StoreId);

        if (string.IsNullOrEmpty(storefrontUrl))
        {
            logger.LogError("Punchout integration '{IntegrationId}' refers to store '{StoreId}', which has no storefront URL.",
                integration.Id, integration.StoreId);

            return PunchoutSetupResult.Error(PunchoutSetupStatus.StoreNotConfigured);
        }

        var session = CreateSession(punchoutSetupContext, integration, storefrontUrl);

        await sessionService.SaveChangesAsync([session]);

        return PunchoutSetupResult.Success(session.StartPage);
    }

    protected virtual async Task<PunchoutIntegration> FindIntegrationAsync(string senderIdentity)
    {
        if (string.IsNullOrEmpty(senderIdentity))
        {
            return null;
        }

        var criteria = AbstractTypeFactory<PunchoutIntegrationSearchCriteria>.TryCreateInstance();
        criteria.SenderIdentity = senderIdentity;
        criteria.IsActive = true;
        criteria.Take = 1;

        var searchResult = await integrationSearchService.SearchNoCloneAsync(criteria);

        return searchResult.Results.FirstOrDefault();
    }

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

    protected virtual PunchoutSession CreateSession(PunchoutSetupContext context, PunchoutIntegration integration, string storefrontUrl)
    {
        var session = AbstractTypeFactory<PunchoutSession>.TryCreateInstance();

        session.StoreId = integration.StoreId;
        session.IntegrationId = integration.Id;
        session.SessionToken = CreateSessionToken();
        session.BuyerCookie = context.BuyerCookie;
        session.BuyerIdentity = context.From;
        session.BuyerDomain = context.FromDomain;
        session.ReturnUrl = context.ReturnUrl;
        session.Status = ModuleConstants.SessionStatus.Created;
        session.ExpirationDate = DateTime.UtcNow.Add(SessionLifetime);
        session.StartPage = BuildStartPage(storefrontUrl, session.SessionToken);

        return session;
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
