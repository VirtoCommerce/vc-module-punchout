using System;
using System.Linq;
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
        // TODO: replace with a signed crypto token.
        session.SessionToken = Guid.NewGuid().ToString("N");
        session.BuyerCookie = context.BuyerCookie;
        session.BuyerIdentity = context.From;
        session.BuyerDomain = context.FromDomain;
        session.ReturnUrl = context.ReturnUrl;
        session.Status = ModuleConstants.SessionStatus.Created;
        session.StartPage = BuildStartPage(storefrontUrl, session.SessionToken);

        return session;
    }

    protected virtual string BuildStartPage(string storefrontUrl, string sessionToken)
    {
        return $"{storefrontUrl.TrimEnd('/')}/{StartPagePath}/{sessionToken}";
    }
}
