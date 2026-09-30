using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Punchout.Core;
using VirtoCommerce.Punchout.Core.Models;
using VirtoCommerce.Punchout.Core.Security;
using VirtoCommerce.Punchout.Core.Services;
using VirtoCommerce.StoreModule.Core.Services;

namespace VirtoCommerce.Punchout.Data.Services;

public class PunchoutSetupService(
    IOptions<PunchoutOptions> options,
    IPunchoutUserMappingSearchService userMappingSearchService,
    IPunchoutSessionService sessionService,
    IStoreService storeService,
    ILogger<PunchoutSetupService> logger)
    : PunchoutSetupServiceBase(storeService), IPunchoutSetupService
{
    protected PunchoutOptions Options => options.Value;

    public override async Task<PunchoutSetupResult> ProcessAsync(PunchoutSetupContext punchoutSetupContext)
    {
        ArgumentNullException.ThrowIfNull(punchoutSetupContext);

        var validationResult = await ValidateAsync(punchoutSetupContext);

        if (!validationResult.IsValid)
        {
            return validationResult.Error;
        }

        var sessionToken = CreateSessionToken();
        var session = CreateSession(punchoutSetupContext, validationResult.UserMapping, validationResult.Configuration, validationResult.StorefrontUrl, sessionToken);
        await sessionService.SaveChangesAsync([session]);

        // The token goes to the buyer in the start page URL, only the hash is stored
        return PunchoutSetupResult.Success(BuildStartPageUrl(session.StartPage, sessionToken));
    }

    protected virtual async Task<PunchoutSetupValidationResult> ValidateAsync(PunchoutSetupContext punchoutSetupContext)
    {
        if (Options.Configurations.IsNullOrEmpty())
        {
            logger.LogError("Punchout is not configured: the '{Section}' configuration section has no {Configurations}.",
                ModuleConstants.ConfigurationSections.ConfigurationKey,
                nameof(Options.Configurations));

            return PunchoutSetupValidationResult.Invalid(PunchoutSetupStatus.StoreNotConfigured);
        }

        var settings = FindConfiguration(punchoutSetupContext);

        if (settings is null)
        {
            logger.LogWarning("Punchout setup rejected for sender identity '{SenderIdentity}': no configuration matches the shared secret.",
                punchoutSetupContext.Sender);

            return PunchoutSetupValidationResult.Invalid(PunchoutSetupStatus.InvalidCredentials);
        }

        if (string.IsNullOrEmpty(settings.StoreId))
        {
            logger.LogError("Punchout is not configured: the configuration matched for sender identity '{SenderIdentity}' has no {Missing}.",
                punchoutSetupContext.Sender,
                nameof(settings.StoreId));

            return PunchoutSetupValidationResult.Invalid(PunchoutSetupStatus.StoreNotConfigured);
        }

        if (!AreCredentialsValid(punchoutSetupContext, settings))
        {
            logger.LogWarning("Punchout setup rejected for sender identity '{SenderIdentity}': invalid credentials.",
                punchoutSetupContext.Sender);

            return PunchoutSetupValidationResult.Invalid(PunchoutSetupStatus.InvalidCredentials);
        }

        if (!IsReturnUrlAllowed(punchoutSetupContext.ReturnUrl, settings))
        {
            logger.LogWarning("Punchout setup rejected for sender identity '{SenderIdentity}': the return URL '{ReturnUrl}' is not allowed.",
                punchoutSetupContext.Sender, punchoutSetupContext.ReturnUrl);

            return PunchoutSetupValidationResult.Invalid(PunchoutSetupStatus.ReturnUrlNotAllowed);
        }

        var userMapping = await FindUserMappingAsync(punchoutSetupContext.Sender);

        if (userMapping is null)
        {
            logger.LogWarning("Punchout setup rejected: sender identity '{SenderIdentity}' is not linked to any user.",
                punchoutSetupContext.Sender);

            return PunchoutSetupValidationResult.Invalid(PunchoutSetupStatus.UserNotFound);
        }

        var storefrontUrl = await GetStorefrontUrlAsync(settings.StoreId);

        if (string.IsNullOrEmpty(storefrontUrl))
        {
            logger.LogError("Punchout is configured for store '{StoreId}', which does not exist or has no storefront URL.",
                settings.StoreId);

            return PunchoutSetupValidationResult.Invalid(PunchoutSetupStatus.StoreNotConfigured);
        }

        return PunchoutSetupValidationResult.Valid(settings, userMapping, storefrontUrl);
    }

    protected virtual PunchoutConfiguration FindConfiguration(PunchoutSetupContext context)
    {
        if (context.SharedSecret.IsNullOrEmpty())
        {
            return null;
        }

        var secret = Encoding.UTF8.GetBytes(context.SharedSecret);
        PunchoutConfiguration result = null;

        // Compare with every configuration, so the response time does not depend on the matched position
        foreach (var configuration in Options.Configurations.Where(x => !x.SharedSecret.IsNullOrEmpty()))
        {
            if (CryptographicOperations.FixedTimeEquals(secret, Encoding.UTF8.GetBytes(configuration.SharedSecret)))
            {
                result ??= configuration;
            }
        }

        return result;
    }

    protected virtual bool AreCredentialsValid(PunchoutSetupContext context, PunchoutConfiguration settings)
    {
        // An empty configured domain is not checked
        return settings.SenderDomain.IsNullOrEmpty() ||
               settings.SenderDomain.EqualsIgnoreCase(context.SenderDomain);
    }

    protected virtual bool IsReturnUrlAllowed(string returnUrl, PunchoutConfiguration settings)
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
        PunchoutConfiguration settings,
        string storefrontUrl,
        string sessionToken)
    {
        var session = AbstractTypeFactory<PunchoutSession>.TryCreateInstance();

        session.StoreId = settings.StoreId;
        session.UserId = userMapping.UserId;
        session.SessionTokenHash = SessionTokenHasher.Hash(sessionToken);
        session.BuyerCookie = context.BuyerCookie;
        session.BuyerIdentity = context.From;
        session.BuyerDomain = context.FromDomain;
        session.ReturnUrl = context.ReturnUrl;
        session.Status = ModuleConstants.SessionStatus.Active;
        session.ExpirationDate = DateTime.UtcNow.Add(settings.SessionLifeTime ?? PunchoutConfiguration.DefaultSessionLifeTime);
        session.TokenExpirationDate = DateTime.UtcNow.Add(settings.TokenLifeTime ?? PunchoutConfiguration.DefaultTokenLifeTime);
        session.StartPage = BuildStartPage(storefrontUrl);

        return session;
    }
}
