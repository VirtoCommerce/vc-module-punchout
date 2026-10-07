using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.Punchout.Core;
using VirtoCommerce.Punchout.Core.Models;
using VirtoCommerce.Punchout.Core.Services;
using VirtoCommerce.StoreModule.Core.Model;
using VirtoCommerce.StoreModule.Core.Services;

namespace VirtoCommerce.Punchout.Data.Services;

public class PunchoutOrderMessageProcessor(
    IOptions<PunchoutOptions> options,
    IPunchoutSessionService sessionService,
    IStoreService storeService,
    IPunchoutHandlerFactory handlerFactory,
    ILogger<PunchoutOrderMessageProcessor> logger)
    : IPunchoutOrderMessageProcessor
{
    protected const string FormField = "cxml-urlencoded";

    protected PunchoutOptions Options => options.Value;

    public virtual async Task<PunchoutCheckoutResult> ProcessAsync(PunchoutOrderMessageRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        // No session id if the access token is not issued by the punchout grant
        var session = request.SessionId.IsNullOrEmpty()
            ? null
            : await sessionService.GetNoCloneAsync(request.SessionId);

        if (!IsSessionOwner(session, request))
        {
            logger.LogWarning("Punchout order message rejected: session '{SessionId}' is not found for user '{UserId}' in store '{StoreId}'.",
                request.SessionId, request.UserId, request.StoreId);

            return CreateErrorResult(PunchoutOrderMessageStatus.SessionNotFound);
        }

        if (!IsSessionActive(session))
        {
            logger.LogWarning("Punchout order message rejected: session '{SessionId}' is expired.",
                session.Id);

            return CreateErrorResult(PunchoutOrderMessageStatus.SessionExpired);
        }

        var configuration = FindConfiguration(session);

        if (configuration is null)
        {
            logger.LogError("Punchout order message rejected: configuration '{ConfigurationId}' of session '{SessionId}' is not found.",
                session.ConfigurationId, session.Id);

            return CreateErrorResult(PunchoutOrderMessageStatus.Error);
        }

        var store = await storeService.GetNoCloneAsync(session.StoreId);

        if (store is null || !IsPunchoutEnabled(store))
        {
            logger.LogWarning("Punchout order message rejected for session '{SessionId}': store '{StoreId}' does not exist or punchout is disabled for it.",
                session.Id, session.StoreId);

            return CreateErrorResult(PunchoutOrderMessageStatus.Error);
        }

        var handlerContext = CreateHandlerContext(request, session, configuration, store);
        var handler = handlerFactory.Create(configuration);
        await handler.HandleOrderMessageAsync(handlerContext);

        if (handlerContext.IsFailed)
        {
            logger.LogWarning("Punchout order message for session '{SessionId}' failed with status '{Status}': {Message}",
                session.Id, handlerContext.ErrorCode, handlerContext.ErrorMessage);

            return CreateErrorResult(PunchoutOrderMessageStatus.Error);
        }

        return handlerContext.Result;
    }

    /// <summary>
    /// The buyer gets a generic error code, the details are only logged.
    /// </summary>
    protected virtual PunchoutCheckoutResult CreateErrorResult(string errorCode)
    {
        var result = AbstractTypeFactory<PunchoutCheckoutResult>.TryCreateInstance();

        result.ErrorCode = errorCode;

        return result;
    }

    protected virtual bool IsSessionOwner(PunchoutSession session, PunchoutOrderMessageRequest request)
    {
        return session is not null &&
               session.UserId.EqualsIgnoreCase(request.UserId) &&
               session.StoreId.EqualsIgnoreCase(request.StoreId);
    }

    protected virtual bool IsSessionActive(PunchoutSession session)
    {
        return session.Status == ModuleConstants.SessionStatus.Active &&
               session.ExpirationDate > DateTime.UtcNow;
    }

    protected virtual PunchoutConfiguration FindConfiguration(PunchoutSession session)
    {
        if (string.IsNullOrEmpty(session.ConfigurationId))
        {
            return null;
        }

        return Options.Configurations.FirstOrDefault(x =>
            x.Id.EqualsIgnoreCase(session.ConfigurationId) &&
            x.StoreId.EqualsIgnoreCase(session.StoreId));
    }

    protected virtual bool IsPunchoutEnabled(Store store)
    {
        return store.Settings.GetValue<bool>(ModuleConstants.Settings.General.PunchoutEnabled);
    }

    protected virtual PunchoutOrderMessageHandlerContext CreateHandlerContext(
        PunchoutOrderMessageRequest request,
        PunchoutSession session,
        PunchoutConfiguration configuration,
        Store store)
    {
        var handlerRequest = AbstractTypeFactory<PunchoutOrderMessageRequest>.TryCreateInstance();

        handlerRequest.SessionId = session.Id;
        handlerRequest.StoreId = session.StoreId;
        handlerRequest.UserId = request.UserId;
        handlerRequest.OrganizationId = request.OrganizationId;
        handlerRequest.CurrencyCode = request.CurrencyCode ?? store.DefaultCurrency;
        handlerRequest.CultureName = request.CultureName ?? store.DefaultLanguage;

        var result = AbstractTypeFactory<PunchoutCheckoutResult>.TryCreateInstance();

        result.Url = session.ReturnUrl;
        result.FormField = FormField;

        var context = AbstractTypeFactory<PunchoutOrderMessageHandlerContext>.TryCreateInstance();

        context.Request = handlerRequest;
        context.Session = session;
        context.Configuration = configuration;
        context.Result = result;

        return context;
    }
}
