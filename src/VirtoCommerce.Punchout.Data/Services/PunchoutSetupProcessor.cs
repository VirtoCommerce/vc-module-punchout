using System;
using System.Buffers.Text;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.Punchout.Core;
using VirtoCommerce.Punchout.Core.Models;
using VirtoCommerce.Punchout.Core.Security;
using VirtoCommerce.Punchout.Core.Services;
using VirtoCommerce.StoreModule.Core.Model;
using VirtoCommerce.StoreModule.Core.Services;

namespace VirtoCommerce.Punchout.Data.Services;

public class PunchoutSetupProcessor(
    IOptions<PunchoutOptions> options,
    IPunchoutUserMappingSearchService userMappingSearchService,
    IPunchoutSessionService sessionService,
    IStoreService storeService,
    IPunchoutHandlerFactory handlerFactory,
    ILogger<PunchoutSetupProcessor> logger)
    : IPunchoutSetupProcessor
{
    protected const string StartPagePath = "punchout";

    protected const int SessionTokenByteCount = 32;

    protected PunchoutOptions Options => options.Value;

    public virtual async Task<PunchoutSetupResult> ProcessAsync(PunchoutSetupRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validationResult = await ValidateAsync(request);

        if (!validationResult.IsValid)
        {
            return validationResult.Error;
        }

        var sessionToken = CreateSessionToken();
        var session = CreateSession(request, validationResult.UserMapping, validationResult.Configuration, validationResult.StorefrontUrl, sessionToken);

        var handlerContext = CreateHandlerContext(request, validationResult, session, sessionToken);
        var handler = handlerFactory.Create(validationResult.Configuration);
        await handler.HandleSetupAsync(handlerContext);

        if (handlerContext.IsFailed)
        {
            return PunchoutSetupResult.Error(handlerContext.ErrorCode, handlerContext.ErrorMessage);
        }

        await sessionService.SaveChangesAsync([handlerContext.Session]);

        return PunchoutSetupResult.Success(handlerContext.StartPage);
    }

    protected virtual PunchoutSetupHandlerContext CreateHandlerContext(
        PunchoutSetupRequest request,
        PunchoutSetupValidationResult validationResult,
        PunchoutSession session,
        string sessionToken)
    {
        var context = AbstractTypeFactory<PunchoutSetupHandlerContext>.TryCreateInstance();

        context.Request = request;
        context.Configuration = validationResult.Configuration;
        context.UserMapping = validationResult.UserMapping;
        context.Session = session;
        // The token goes to the buyer in the start page URL, only the hash is stored
        context.StartPage = BuildStartPageUrl(session.StartPage, sessionToken);

        return context;
    }

    protected virtual async Task<PunchoutSetupValidationResult> ValidateAsync(PunchoutSetupRequest request)
    {
        if (Options.Configurations.IsNullOrEmpty())
        {
            logger.LogError("Punchout is not configured: the '{Section}' configuration section has no {Configurations}.",
                ModuleConstants.ConfigurationSections.ConfigurationKey,
                nameof(Options.Configurations));

            return PunchoutSetupValidationResult.Invalid(PunchoutSetupStatus.InvalidCredentials);
        }

        var settings = FindConfiguration(request);

        if (settings is null)
        {
            logger.LogWarning("Punchout setup rejected for sender identity '{SenderIdentity}': no configuration matches the shared secret.",
                request.Sender);

            return PunchoutSetupValidationResult.Invalid(PunchoutSetupStatus.InvalidCredentials);
        }

        if (string.IsNullOrEmpty(settings.Id) || string.IsNullOrEmpty(settings.StoreId))
        {
            logger.LogError("Punchout is not configured: the configuration matched for sender identity '{SenderIdentity}' has no {Missing}.",
                request.Sender,
                string.IsNullOrEmpty(settings.Id) ? nameof(settings.Id) : nameof(settings.StoreId));

            return PunchoutSetupValidationResult.Invalid(PunchoutSetupStatus.StoreNotConfigured);
        }

        if (!AreCredentialsValid(request, settings))
        {
            logger.LogWarning("Punchout setup rejected for sender identity '{SenderIdentity}': invalid credentials.",
                request.Sender);

            return PunchoutSetupValidationResult.Invalid(PunchoutSetupStatus.InvalidCredentials);
        }

        if (!IsReturnUrlAllowed(request.ReturnUrl, settings))
        {
            logger.LogWarning("Punchout setup rejected for sender identity '{SenderIdentity}': the return URL '{ReturnUrl}' is not allowed.",
                request.Sender, request.ReturnUrl);

            return PunchoutSetupValidationResult.Invalid(PunchoutSetupStatus.ReturnUrlNotAllowed);
        }

        var userMapping = await FindUserMappingAsync(request.Sender);

        if (userMapping is null)
        {
            logger.LogWarning("Punchout setup rejected: sender identity '{SenderIdentity}' is not linked to any user.",
                request.Sender);

            return PunchoutSetupValidationResult.Invalid(PunchoutSetupStatus.UserNotFound);
        }

        var store = await storeService.GetByIdAsync(settings.StoreId);

        if (store is null)
        {
            logger.LogError("Punchout is configured for store '{StoreId}', which does not exist.",
                settings.StoreId);

            return PunchoutSetupValidationResult.Invalid(PunchoutSetupStatus.StoreNotConfigured);
        }

        if (!IsPunchoutEnabled(store))
        {
            logger.LogWarning("Punchout setup rejected for sender identity '{SenderIdentity}': punchout is disabled for store '{StoreId}'.",
                request.Sender, store.Id);

            return PunchoutSetupValidationResult.Invalid(PunchoutSetupStatus.StoreNotConfigured);
        }

        var storefrontUrl = GetStorefrontUrl(store);

        if (string.IsNullOrEmpty(storefrontUrl))
        {
            logger.LogError("Punchout is configured for store '{StoreId}', which has no storefront URL.",
                settings.StoreId);

            return PunchoutSetupValidationResult.Invalid(PunchoutSetupStatus.StoreNotConfigured);
        }

        return PunchoutSetupValidationResult.Valid(settings, userMapping, storefrontUrl);
    }

    protected virtual PunchoutConfiguration FindConfiguration(PunchoutSetupRequest request)
    {
        if (request.SharedSecret.IsNullOrEmpty())
        {
            return null;
        }

        var secret = Encoding.UTF8.GetBytes(request.SharedSecret);
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

    protected virtual bool AreCredentialsValid(PunchoutSetupRequest request, PunchoutConfiguration settings)
    {
        // An empty configured domain is not checked
        return settings.SenderDomain.IsNullOrEmpty() ||
               settings.SenderDomain.EqualsIgnoreCase(request.SenderDomain);
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
        PunchoutSetupRequest request,
        PunchoutUserMapping userMapping,
        PunchoutConfiguration settings,
        string storefrontUrl,
        string sessionToken)
    {
        var session = AbstractTypeFactory<PunchoutSession>.TryCreateInstance();

        session.StoreId = settings.StoreId;
        session.ConfigurationId = settings.Id;
        session.UserId = userMapping.UserId;
        session.SessionTokenHash = SessionTokenHasher.Hash(sessionToken);
        session.BuyerCookie = request.BuyerCookie;
        session.BuyerIdentity = request.From;
        session.BuyerDomain = request.FromDomain;
        session.SupplierIdentity = request.To;
        session.SupplierDomain = request.ToDomain;
        session.ReturnUrl = request.ReturnUrl;
        session.Status = ModuleConstants.SessionStatus.Active;
        session.ExpirationDate = DateTime.UtcNow.Add(settings.SessionLifeTime ?? PunchoutConfiguration.DefaultSessionLifeTime);
        session.TokenExpirationDate = DateTime.UtcNow.Add(settings.TokenLifeTime ?? PunchoutConfiguration.DefaultTokenLifeTime);
        session.StartPage = BuildStartPage(storefrontUrl);

        return session;
    }

    protected virtual bool IsPunchoutEnabled(Store store)
    {
        return store.Settings.GetValue<bool>(ModuleConstants.Settings.General.PunchoutEnabled);
    }

    protected virtual string GetStorefrontUrl(Store store)
    {
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
