using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Punchout.Core;
using VirtoCommerce.Punchout.Core.Extensions;
using VirtoCommerce.Punchout.Core.Models;
using VirtoCommerce.Punchout.Core.Services;
using VirtoCommerce.StoreModule.Core.Services;

namespace VirtoCommerce.Punchout.Data.Services;

public class PunchoutOrderMessageProcessor(
    IOptions<PunchoutOptions> options,
    IPunchoutSessionService sessionService,
    IPunchoutSessionManagementService sessionManagementService,
    IPunchoutOrderMessageService orderMessageService,
    IStoreService storeService,
    IPunchoutHandlerFactory handlerFactory,
    ILogger<PunchoutOrderMessageProcessor> logger)
    : IPunchoutOrderMessageProcessor
{
    private const string FormField = "cxml-urlencoded";

    protected PunchoutOptions Options => options.Value;

    public virtual async Task<PunchoutCheckoutResult> ProcessAsync(PunchoutOrderMessageRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validationResult = await ValidateAsync(request);

        if (!validationResult.IsValid)
        {
            return CreateErrorResult(validationResult.ErrorCode);
        }

        var session = validationResult.Session;

        var handler = handlerFactory.Create(validationResult.Configuration);
        if (handler == null)
        {
            return CreateErrorResult(PunchoutOrderMessageStatus.ConfigurationError);
        }

        var handlerContext = CreateHandlerContext(request, validationResult);
        try
        {
            await handler.HandleOrderMessageAsync(handlerContext);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Punchout '{HandlerName}' handler failed to process order message for session '{SessionId}'.",
                handler.GetType().Name, session.Id);

            return CreateErrorResult(PunchoutOrderMessageStatus.MessageError);
        }

        if (handlerContext.IsFailed)
        {
            logger.LogWarning("Punchout order message for session '{SessionId}' failed with status '{Status}': {Message}",
                session.Id, handlerContext.ErrorCode, handlerContext.ErrorMessage);

            return CreateErrorResult(handlerContext.ErrorCode);
        }

        if (handlerContext.Result == null || handlerContext.Result.Cxml.IsNullOrEmpty())
        {
            logger.LogWarning("Punchout '{HandlerName}' handler did not set result Cxml", handler.GetType().Name);

            return CreateErrorResult(PunchoutOrderMessageStatus.MessageError);
        }

        // One session transfers a requisition only once
        if (!await sessionManagementService.ReturnSessionAsync(session.Id))
        {
            logger.LogWarning("Punchout order message rejected: session '{SessionId}' is already returned or expired.",
                session.Id);

            return CreateErrorResult(PunchoutOrderMessageStatus.SessionExpired);
        }

        // Keep the log of what is sent to the buyer
        var orderMessage = CreateOrderMessage(handlerContext);
        await orderMessageService.SaveChangesAsync([orderMessage]);

        return handlerContext.Result;
    }

    protected virtual async Task<PunchoutOrderMessageValidationResult> ValidateAsync(PunchoutOrderMessageRequest request)
    {
        // No session id if the access token is not issued by the punchout grant
        var session = request.SessionId.IsNullOrEmpty()
            ? null
            : await sessionService.GetByIdAsync(request.SessionId);

        if (!IsSessionOwner(session, request))
        {
            logger.LogWarning("Punchout order message rejected: session '{SessionId}' is not found for user '{UserId}' in store '{StoreId}'.",
                request.SessionId, request.UserId, request.StoreId);

            return PunchoutOrderMessageValidationResult.Invalid(PunchoutOrderMessageStatus.SessionNotFound);
        }

        if (!IsSessionActive(session))
        {
            logger.LogWarning("Punchout order message rejected: session '{SessionId}' is expired.",
                session.Id);

            return PunchoutOrderMessageValidationResult.Invalid(PunchoutOrderMessageStatus.SessionExpired);
        }

        var configuration = FindConfiguration(session);

        if (configuration is null)
        {
            logger.LogWarning("Configuration '{ConfigurationId}' of session '{SessionId}' was not found.",
                session.ConfigurationId, session.Id);

            return PunchoutOrderMessageValidationResult.Invalid(PunchoutOrderMessageStatus.ConfigurationError);
        }

        var store = await storeService.GetPunchoutStoreAsync(session.StoreId);

        if (store is null)
        {
            logger.LogWarning("Punchout order message rejected for session '{SessionId}': store '{StoreId}' does not exist or punchout is disabled for it.",
                session.Id, session.StoreId);

            return PunchoutOrderMessageValidationResult.Invalid(PunchoutOrderMessageStatus.StoreNotConfigured);
        }

        return PunchoutOrderMessageValidationResult.Valid(session, configuration, store);
    }

    protected virtual PunchoutOrderMessage CreateOrderMessage(PunchoutOrderMessageHandlerContext context)
    {
        var orderMessage = AbstractTypeFactory<PunchoutOrderMessage>.TryCreateInstance();

        orderMessage.SessionId = context.Session.Id;
        orderMessage.Cxml = context.Result.Cxml;

        return orderMessage;
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

    protected virtual PunchoutOrderMessageHandlerContext CreateHandlerContext(
        PunchoutOrderMessageRequest request,
        PunchoutOrderMessageValidationResult validationResult)
    {
        var session = validationResult.Session;
        var store = validationResult.Store;

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
        context.Configuration = validationResult.Configuration;
        context.Result = result;

        return context;
    }
}
