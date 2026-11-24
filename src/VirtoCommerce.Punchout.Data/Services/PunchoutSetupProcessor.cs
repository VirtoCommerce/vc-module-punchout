using System;
using System.Buffers.Text;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Punchout.Core;
using VirtoCommerce.Punchout.Core.Cxml;
using VirtoCommerce.Punchout.Core.Extensions;
using VirtoCommerce.Punchout.Core.Models;
using VirtoCommerce.Punchout.Core.Security;
using VirtoCommerce.Punchout.Core.Services;
using VirtoCommerce.StoreModule.Core.Model;
using VirtoCommerce.StoreModule.Core.Services;

namespace VirtoCommerce.Punchout.Data.Services;

public partial class PunchoutSetupProcessor(
    IOptions<PunchoutOptions> options,
    IPunchoutUserMappingSearchService userMappingSearchService,
    IPunchoutSessionService sessionService,
    IStoreService storeService,
    IPunchoutHandlerFactory handlerFactory,
    IPunchoutOrganizationUserResolver organizationUserResolver,
    ILogger<PunchoutSetupProcessor> logger)
    : BasePunchoutProcessor(options, userMappingSearchService), IPunchoutSetupProcessor
{
    protected const string StartPagePath = "punchout";

    protected const int SessionTokenByteCount = 32;

    public virtual async Task<PunchoutSetupResult> ProcessAsync(PunchoutSetupRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validationResult = await ValidateAsync(request);

        if (!validationResult.IsValid)
        {
            return validationResult.Error;
        }

        var sessionToken = CreateSessionToken();
        var session = CreateSession(request, validationResult, sessionToken);

        var handler = handlerFactory.Create(validationResult.Configuration);
        if (handler == null)
        {
            return PunchoutSetupResult.Error(PunchoutSetupStatus.ConfigurationError);
        }

        var handlerContext = CreateHandlerContext(request, validationResult, session, sessionToken);
        try
        {
            await handler.HandleSetupAsync(handlerContext);
        }
        catch (Exception ex)
        {
            LogHandlerFailed(ex, handler.GetType().Name, request.Sender);

            return PunchoutSetupResult.Error(PunchoutSetupStatus.SetupError);
        }

        if (handlerContext.IsFailed)
        {
            LogSetupFailed(handler.GetType().Name, session.Id, handlerContext.ErrorCode, handlerContext.ErrorMessage);

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
        context.Store = validationResult.Store;
        context.Session = session;

        // The token goes to the buyer in the start page URL, only the hash is stored
        context.StartPage = BuildStartPageUrl(session.StartPage, sessionToken);

        return context;
    }

    protected virtual async Task<PunchoutSetupValidationResult> ValidateAsync(PunchoutSetupRequest request)
    {
        if (Options.Configurations.IsNullOrEmpty())
        {
            LogNoConfigurations(ModuleConstants.ConfigurationSections.ConfigurationKey, nameof(Options.Configurations));

            return PunchoutSetupValidationResult.Invalid(PunchoutSetupStatus.InvalidCredentials);
        }

        var settings = FindConfiguration(request);

        if (settings is null)
        {
            LogConfigurationNotMatched(request.Sender);

            return PunchoutSetupValidationResult.Invalid(PunchoutSetupStatus.InvalidCredentials);
        }

        if (settings.Id.IsNullOrEmpty() || settings.StoreId.IsNullOrEmpty())
        {
            LogConfigurationIncomplete(request.Sender, string.IsNullOrEmpty(settings.Id) ? nameof(settings.Id) : nameof(settings.StoreId));

            return PunchoutSetupValidationResult.Invalid(PunchoutSetupStatus.StoreNotConfigured);
        }

        if (!AreCredentialsValid(request, settings))
        {
            LogInvalidCredentials(request.Sender);

            return PunchoutSetupValidationResult.Invalid(PunchoutSetupStatus.InvalidCredentials);
        }

        if (!IsReturnUrlAllowed(request.ReturnUrl, settings))
        {
            LogReturnUrlNotAllowed(request.Sender, request.ReturnUrl);

            return PunchoutSetupValidationResult.Invalid(PunchoutSetupStatus.ReturnUrlNotAllowed);
        }

        var mappingIdentity = GetMappingIdentity(request);
        var userMapping = await FindUserMappingAsync(mappingIdentity);

        if (userMapping is null)
        {
            LogUserNotFound(mappingIdentity);

            return PunchoutSetupValidationResult.Invalid(PunchoutSetupStatus.UserNotFound);
        }

        var userId = userMapping.UserId;
        string organizationId = null;

        if (userMapping.IsOrganizationMapping)
        {
            var userResult = await ResolveOrganizationUserAsync(request, userMapping, settings);

            if (userResult.Error != null)
            {
                return userResult;
            }

            userId = userResult.UserId;
            organizationId = userMapping.OrganizationId;
        }

        var store = await storeService.GetPunchoutStoreAsync(settings.StoreId);

        if (store is null)
        {
            LogStoreNotConfigured(request.Sender, settings.StoreId);

            return PunchoutSetupValidationResult.Invalid(PunchoutSetupStatus.StoreNotConfigured);
        }

        var storefrontUrl = GetStorefrontUrl(store);

        if (string.IsNullOrEmpty(storefrontUrl))
        {
            LogStorefrontUrlMissing(settings.StoreId);

            return PunchoutSetupValidationResult.Invalid(PunchoutSetupStatus.StoreNotConfigured);
        }

        return PunchoutSetupValidationResult.Valid(settings, userMapping, userId, organizationId, store, storefrontUrl);
    }

    /// <summary>
    /// Finds the user of the mapped organization by the email of the end user. Returns either an error or the user id.
    /// </summary>
    protected virtual async Task<PunchoutSetupValidationResult> ResolveOrganizationUserAsync(
        PunchoutSetupRequest request,
        PunchoutUserMapping userMapping,
        PunchoutConfiguration settings)
    {
        var email = GetUserEmail(request);

        if (string.IsNullOrWhiteSpace(email))
        {
            LogUserEmailMissing(userMapping.ExternalId, userMapping.OrganizationId);

            return PunchoutSetupValidationResult.Invalid(PunchoutSetupStatus.InvalidRequest, "The request contains no end user email.");
        }

        var userRequest = AbstractTypeFactory<PunchoutOrganizationUserRequest>.TryCreateInstance();
        userRequest.OrganizationId = userMapping.OrganizationId;
        userRequest.Email = email;
        userRequest.StoreId = settings.StoreId;

        var result = await organizationUserResolver.ResolveAsync(userRequest);

        switch (result.Status)
        {
            case PunchoutOrganizationUserStatus.Found:
                return new PunchoutSetupValidationResult { UserId = result.User.Id };

            case PunchoutOrganizationUserStatus.Ambiguous:
                LogAmbiguousUser(email, userMapping.OrganizationId, result.UserMatchCount);

                return PunchoutSetupValidationResult.Invalid(PunchoutSetupStatus.AmbiguousUser);

            default:
                LogOrganizationUserNotFound(email, userMapping.OrganizationId);

                return PunchoutSetupValidationResult.Invalid(PunchoutSetupStatus.UserNotFound);
        }
    }

    /// <summary>
    /// The email of the end user: Contact role="endUser" or, when the contact has no email, the UserEmail extrinsic.
    /// </summary>
    protected virtual string GetUserEmail(PunchoutSetupRequest request)
    {
        return GetUserEmail(request.User, request.Extrinsics);
    }

    protected virtual bool IsReturnUrlAllowed(string returnUrl, PunchoutConfiguration settings)
    {
        if (settings.AllowedReturnUrls.IsNullOrEmpty())
        {
            return true;
        }

        if (returnUrl.IsNullOrEmpty())
        {
            return false;
        }

        return settings.AllowedReturnUrls
            .Where(x => !x.IsNullOrWhiteSpace())
            .Any(pattern => pattern.EndsWith('*')
                ? returnUrl.StartsWith(pattern[..^1], StringComparison.OrdinalIgnoreCase)
                : returnUrl.EqualsIgnoreCase(pattern));
    }

    protected virtual PunchoutSession CreateSession(
        PunchoutSetupRequest request,
        PunchoutSetupValidationResult validationResult,
        string sessionToken)
    {
        var settings = validationResult.Configuration;
        var session = AbstractTypeFactory<PunchoutSession>.TryCreateInstance();

        session.StoreId = settings.StoreId;
        session.ConfigurationId = settings.Id;
        session.UserId = validationResult.UserId;
        session.UserMappingId = validationResult.UserMapping.Id;
        session.OrganizationId = validationResult.OrganizationId;
        session.SessionTokenHash = SessionTokenHasher.Hash(sessionToken);
        session.BuyerCookie = request.BuyerCookie;
        session.BuyerIdentity = request.From;
        session.BuyerDomain = request.FromDomain;
        session.SupplierIdentity = request.To;
        session.SupplierDomain = request.ToDomain;
        session.SenderIdentity = request.Sender;
        session.SenderDomain = request.SenderDomain;
        session.ReturnUrl = request.ReturnUrl;
        session.Status = ModuleConstants.SessionStatus.Active;
        session.ExpirationDate = DateTime.UtcNow.Add(settings.SessionLifeTime ?? PunchoutConfiguration.DefaultSessionLifeTime);
        session.TokenExpirationDate = DateTime.UtcNow.Add(settings.TokenLifeTime ?? PunchoutConfiguration.DefaultTokenLifeTime);
        session.StartPage = BuildStartPage(validationResult.StorefrontUrl);

        return session;
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

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Punchout '{HandlerName}' handler failed to process setup for sender identity '{SenderIdentity}'.")]
    private partial void LogHandlerFailed(Exception ex, string handlerName, string senderIdentity);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Punchout '{HandlerName}' handler failed to process setup for session '{SessionId}' with status '{Status}': {Message}")]
    private partial void LogSetupFailed(string handlerName, string sessionId, string status, string message);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Punchout is not configured: the '{Section}' configuration section has no {Configurations}.")]
    private partial void LogNoConfigurations(string section, string configurations);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Punchout setup rejected for sender identity '{SenderIdentity}': no configuration matches the shared secret.")]
    private partial void LogConfigurationNotMatched(string senderIdentity);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Punchout is not configured: the configuration matched for sender identity '{SenderIdentity}' has no {Missing}.")]
    private partial void LogConfigurationIncomplete(string senderIdentity, string missing);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Punchout setup rejected for sender identity '{SenderIdentity}': invalid credentials.")]
    private partial void LogInvalidCredentials(string senderIdentity);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Punchout setup rejected for sender identity '{SenderIdentity}': the return URL '{ReturnUrl}' is not allowed.")]
    private partial void LogReturnUrlNotAllowed(string senderIdentity, string returnUrl);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Punchout setup rejected: identity '{MappingIdentity}' is not linked to any user or organization.")]
    private partial void LogUserNotFound(string mappingIdentity);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Punchout setup rejected: identity '{MappingIdentity}' is linked to organization '{OrganizationId}', but the request contains no end user email.")]
    private partial void LogUserEmailMissing(string mappingIdentity, string organizationId);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Punchout setup rejected: no user with email '{Email}' is found in organization '{OrganizationId}'.")]
    private partial void LogOrganizationUserNotFound(string email, string organizationId);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Punchout setup rejected: {MatchCount} users with email '{Email}' are found in organization '{OrganizationId}', the user is ambiguous.")]
    private partial void LogAmbiguousUser(string email, string organizationId, int matchCount);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Punchout setup rejected for sender identity '{SenderIdentity}': store '{StoreId}' does not exist or punchout is disabled for it.")]
    private partial void LogStoreNotConfigured(string senderIdentity, string storeId);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Punchout is configured for store '{StoreId}', which has no storefront URL.")]
    private partial void LogStorefrontUrlMissing(string storeId);
}
