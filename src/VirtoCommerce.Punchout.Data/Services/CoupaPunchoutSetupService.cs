using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Punchout.Core;
using VirtoCommerce.Punchout.Core.Coupa;
using VirtoCommerce.Punchout.Core.Models;
using VirtoCommerce.Punchout.Core.Services;
using VirtoCommerce.StoreModule.Core.Services;

namespace VirtoCommerce.Punchout.Data.Services;

public class CoupaPunchoutSetupService(
    IOptions<CoupaConfiguration> configuration,
    IPunchoutUserMappingSearchService userMappingSearchService,
    IPunchoutSessionService sessionService,
    IStoreService storeService,
    ILogger<CoupaPunchoutSetupService> logger)
    : PunchoutSetupServiceBase(storeService), IPunchoutSetupService
{
    protected CoupaConfiguration Configuration => configuration.Value;

    public override async Task<PunchoutSetupResult> ProcessAsync(PunchoutSetupContext punchoutSetupContext)
    {
        ArgumentNullException.ThrowIfNull(punchoutSetupContext);

        var settings = Configuration;

        if (string.IsNullOrEmpty(settings.SharedSecret) || string.IsNullOrEmpty(settings.StoreId))
        {
            logger.LogError("Punchout is not configured: the '{Section}' configuration section has no {Missing}.",
                ModuleConstants.ConfigurationSections.CoupaConfiguration,
                string.IsNullOrEmpty(settings.SharedSecret) ? nameof(settings.SharedSecret) : nameof(settings.StoreId));

            return PunchoutSetupResult.Error(PunchoutSetupStatus.StoreNotConfigured);
        }

        if (!AreCredentialsValid(punchoutSetupContext, settings))
        {
            logger.LogWarning("Punchout setup rejected for sender identity '{SenderIdentity}': invalid credentials.",
                punchoutSetupContext.Sender);

            return PunchoutSetupResult.Error(PunchoutSetupStatus.InvalidCredentials);
        }

        if (!IsReturnUrlAllowed(punchoutSetupContext.ReturnUrl, settings))
        {
            logger.LogWarning("Punchout setup rejected for sender identity '{SenderIdentity}': the return URL '{ReturnUrl}' is not allowed.",
                punchoutSetupContext.Sender, punchoutSetupContext.ReturnUrl);

            return PunchoutSetupResult.Error(PunchoutSetupStatus.ReturnUrlNotAllowed);
        }

        var userMapping = await FindUserMappingAsync(punchoutSetupContext.Sender);

        if (userMapping is null)
        {
            logger.LogWarning("Punchout setup rejected: sender identity '{SenderIdentity}' is not linked to any user.",
                punchoutSetupContext.Sender);

            return PunchoutSetupResult.Error(PunchoutSetupStatus.UserNotFound);
        }

        var storefrontUrl = await GetStorefrontUrlAsync(settings.StoreId);

        if (string.IsNullOrEmpty(storefrontUrl))
        {
            logger.LogError("Punchout is configured for store '{StoreId}', which does not exist or has no storefront URL.",
                settings.StoreId);

            return PunchoutSetupResult.Error(PunchoutSetupStatus.StoreNotConfigured);
        }

        var session = CreateSession(punchoutSetupContext, userMapping, settings, storefrontUrl);

        await sessionService.SaveChangesAsync([session]);

        return PunchoutSetupResult.Success(session.StartPage);
    }

    protected virtual bool AreCredentialsValid(PunchoutSetupContext context, CoupaConfiguration settings)
    {
        // An empty configured domain is not checked
        if (!settings.SenderDomain.IsNullOrEmpty() &&
            !settings.SenderDomain.EqualsIgnoreCase(context.SenderDomain))
        {
            return false;
        }

        return !settings.SenderDomain.IsNullOrEmpty() &&
               CryptographicOperations.FixedTimeEquals(
                   Encoding.UTF8.GetBytes(context.SharedSecret),
                   Encoding.UTF8.GetBytes(settings.SharedSecret));
    }

    protected virtual bool IsReturnUrlAllowed(string returnUrl, CoupaConfiguration settings)
    {
        if (settings.AllowedReturnUrls.IsNullOrEmpty())
        {
            return true;
        }

        if (string.IsNullOrEmpty(returnUrl))
        {
            return false;
        }

        return settings.AllowedReturnUrls
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Any(pattern => pattern.EndsWith('*')
                ? returnUrl.StartsWith(pattern[..^1], StringComparison.OrdinalIgnoreCase)
                : returnUrl.EqualsIgnoreCase(pattern));
    }

    protected virtual async Task<PunchoutUserMapping> FindUserMappingAsync(string senderIdentity)
    {
        if (string.IsNullOrEmpty(senderIdentity))
        {
            return null;
        }

        var criteria = AbstractTypeFactory<PunchoutUserMappingSearchCriteria>.TryCreateInstance();
        criteria.ExternalIds = [senderIdentity];
        criteria.IsActive = true;
        criteria.Take = 1;

        var searchResult = await userMappingSearchService.SearchNoCloneAsync(criteria);

        return searchResult.Results.FirstOrDefault();
    }

    protected virtual PunchoutSession CreateSession(
        PunchoutSetupContext context,
        PunchoutUserMapping userMapping,
        CoupaConfiguration settings,
        string storefrontUrl)
    {
        var session = AbstractTypeFactory<PunchoutSession>.TryCreateInstance();

        session.StoreId = settings.StoreId;
        session.UserId = userMapping.UserId;
        session.SessionToken = CreateSessionToken();
        session.BuyerCookie = context.BuyerCookie;
        session.BuyerIdentity = context.From;
        session.BuyerDomain = context.FromDomain;
        session.ReturnUrl = context.ReturnUrl;
        session.Status = ModuleConstants.SessionStatus.Created;
        session.ExpirationDate = DateTime.UtcNow.Add(settings.TokenLifeTime ?? CoupaConfiguration.DefaultTokenLifeTime);
        session.StartPage = BuildStartPage(storefrontUrl, session.SessionToken);

        return session;
    }
}
