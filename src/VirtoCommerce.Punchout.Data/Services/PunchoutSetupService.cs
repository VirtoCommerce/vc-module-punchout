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

/// <summary>
/// Authenticates a setup request against the punchout integrations stored in the database:
/// the sender identity selects the integration and the shared secret is verified against its hash.
/// </summary>
public class PunchoutSetupService(
    IPunchoutIntegrationSearchService integrationSearchService,
    IPunchoutSecretHasher secretHasher,
    IPunchoutSessionService sessionService,
    IStoreService storeService,
    ILogger<PunchoutSetupService> logger)
    : PunchoutSetupServiceBase(storeService), IPunchoutSetupService
{
    public override async Task<PunchoutSetupResult> ProcessAsync(PunchoutSetupContext punchoutSetupContext)
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
}
