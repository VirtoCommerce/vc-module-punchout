using System;
using System.Linq;
using System.Threading.Tasks;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Punchout.Core;
using VirtoCommerce.Punchout.Core.Cxml;
using VirtoCommerce.Punchout.Core.Extensions;
using VirtoCommerce.Punchout.Core.Models;
using VirtoCommerce.Punchout.Core.Services;
using VirtoCommerce.StoreModule.Core.Model;
using VirtoCommerce.StoreModule.Core.Services;

namespace VirtoCommerce.Punchout.Data.Services;

public class PunchoutOrderRequestProcessor(
    IOptions<PunchoutOptions> options,
    IPunchoutUserMappingSearchService userMappingSearchService,
    IStoreService storeService,
    IPunchoutOrderRequestService orderRequestService,
    IPunchoutOrderRequestSearchService orderRequestSearchService,
    IPunchoutHandlerFactory handlerFactory,
    ILogger<PunchoutOrderRequestProcessor> logger)
    : BasePunchoutProcessor(options, userMappingSearchService), IPunchoutOrderRequestProcessor
{
    protected const int PayloadIdMaxLength = 512;
    protected const int PurchaseOrderNumberMaxLength = 256;
    protected const int IdentityMaxLength = 512;
    protected const int OrderTypeMaxLength = 32;
    protected const int CurrencyMaxLength = 64;
    protected const int ErrorCodeMaxLength = 64;
    protected const int ErrorMessageMaxLength = 2048;

    public virtual async Task<PunchoutOrderResult> ProcessAsync(PunchoutOrderRequestData request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validationResult = await ValidateAsync(request);

        // Requests that are not authenticated are not stored
        if (validationResult.Configuration is null)
        {
            return validationResult.Error;
        }

        // The same document is answered with the stored result, even when it would not pass the validation now
        var existingRequest = await FindExistingRequestAsync(request, validationResult.Configuration);

        if (existingRequest is not null)
        {
            var existingResult = GetExistingRequestResult(request, existingRequest);

            if (existingResult is not null)
            {
                return existingResult;
            }
        }

        var orderRequest = CreateOrderRequest(request, validationResult, existingRequest);

        if (!validationResult.IsValid)
        {
            await SaveOrderRequestAsync(orderRequest, PunchoutOrderRequestStatus.Rejected, validationResult.Error);

            return validationResult.Error;
        }

        var conflictResult = await StartProcessingAsync(request, orderRequest);

        if (conflictResult is not null)
        {
            return conflictResult;
        }

        var result = await HandleOrderAsync(request, validationResult);

        await SaveOrderRequestAsync(orderRequest, GetOrderRequestStatus(result), result);

        return result;
    }

    protected virtual async Task<PunchoutOrderResult> HandleOrderAsync(PunchoutOrderRequestData request, PunchoutOrderValidationResult validationResult)
    {
        var handler = handlerFactory.Create(validationResult.Configuration);
        if (handler == null)
        {
            return PunchoutOrderResult.Error(PunchoutOrderStatus.ConfigurationError);
        }

        var handlerContext = CreateHandlerContext(request, validationResult);
        try
        {
            await handler.HandleOrderRequestAsync(handlerContext);
        }
        catch (ValidationException ex)
        {
            // The cart or the order did not pass the platform validation, e.g. a product is out of stock
            var errorMessage = GetValidationErrorMessage(ex);

            logger.LogWarning("Punchout '{HandlerName}' handler rejected order '{PurchaseOrderNumber}': {Message}",
                handler.GetType().Name, request.OrderId, errorMessage);

            return PunchoutOrderResult.Error(PunchoutOrderStatus.InvalidRequest, errorMessage);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Punchout '{HandlerName}' handler failed to process order '{PurchaseOrderNumber}'",
                handler.GetType().Name, request.OrderId);

            return PunchoutOrderResult.Error(PunchoutOrderStatus.OrderError);
        }

        if (handlerContext.IsFailed)
        {
            logger.LogWarning("Punchout '{HandlerName}' handler failed to process order '{PurchaseOrderNumber}' with status '{Status}': {Message}",
                handler.GetType().Name, request.OrderId, handlerContext.ErrorCode, handlerContext.ErrorMessage);

            return PunchoutOrderResult.Error(handlerContext.ErrorCode, handlerContext.ErrorMessage);
        }

        return PunchoutOrderResult.Success(handlerContext.Result.CustomerOrderId, handlerContext.Result.PurchaseOrderNumber);
    }

    /// <summary>
    /// A handler result that rejects the order content is Rejected, a processing problem is Failed.
    /// </summary>
    protected virtual string GetOrderRequestStatus(PunchoutOrderResult result)
    {
        return result.Status switch
        {
            PunchoutOrderStatus.Success => PunchoutOrderRequestStatus.Completed,
            PunchoutOrderStatus.InvalidRequest => PunchoutOrderRequestStatus.Rejected,
            _ => PunchoutOrderRequestStatus.Failed,
        };
    }

    /// <summary>
    /// Creates the record of the request. A failed or rejected attempt of the same document is processed again in its record,
    /// because the payload id is unique within the configuration.
    /// </summary>
    protected virtual PunchoutOrderRequest CreateOrderRequest(PunchoutOrderRequestData request, PunchoutOrderValidationResult validationResult, PunchoutOrderRequest existingRequest)
    {
        // The existing request comes from the cache and must not be changed
        var orderRequest = existingRequest is not null && existingRequest.PayloadId == request.PayloadId
            ? (PunchoutOrderRequest)existingRequest.Clone()
            : AbstractTypeFactory<PunchoutOrderRequest>.TryCreateInstance();

        var configuration = validationResult.Configuration;

        orderRequest.ConfigurationId = configuration.Id;
        orderRequest.StoreId = configuration.StoreId;
        orderRequest.PayloadId = request.PayloadId;
        orderRequest.SenderIdentity = Truncate(request.Sender, IdentityMaxLength);
        orderRequest.BuyerIdentity = Truncate(request.From, IdentityMaxLength);
        orderRequest.UserId = validationResult.UserMapping?.UserId;
        // The type attribute defaults to 'new' in the cXML DTD
        orderRequest.OrderType = Truncate(string.IsNullOrEmpty(request.OrderType) ? CxmlConstants.OrderRequestType.New : request.OrderType, OrderTypeMaxLength);
        orderRequest.PurchaseOrderNumber = Truncate(request.OrderId, PurchaseOrderNumberMaxLength);
        orderRequest.PurchaseOrderDate = request.OrderDate?.UtcDateTime;
        orderRequest.Currency = Truncate(request.Currency, CurrencyMaxLength);
        orderRequest.Total = request.Total;
        orderRequest.CustomerOrderId = null;

        return orderRequest;
    }

    /// <summary>
    /// Stores the request as Processing. Returns null when the processing can start,
    /// or the result for the same document stored by a concurrent request.
    /// </summary>
    protected virtual async Task<PunchoutOrderResult> StartProcessingAsync(PunchoutOrderRequestData request, PunchoutOrderRequest orderRequest)
    {
        orderRequest.Status = PunchoutOrderRequestStatus.Processing;
        orderRequest.ErrorCode = null;
        orderRequest.ErrorMessage = null;

        try
        {
            await orderRequestService.SaveChangesAsync([orderRequest]);

            return null;
        }
        catch (Exception ex)
        {
            // The unique index on the configuration and payload id rejects the same document inserted by a concurrent request
            var concurrentRequest = await FindRequestByPayloadIdAsync(orderRequest.ConfigurationId, orderRequest.PayloadId);

            if (concurrentRequest is null || concurrentRequest.Id == orderRequest.Id)
            {
                throw;
            }

            logger.LogWarning(ex, "Punchout order request '{OrderId}' with payload '{PayloadId}' is already stored by request '{ExistingRequestId}'.",
                request.OrderId, request.PayloadId, concurrentRequest.Id);

            return GetExistingRequestResult(request, concurrentRequest)
                ?? PunchoutOrderResult.Error(PunchoutOrderStatus.OrderInProgress, "The order is being processed, please retry later.");
        }
    }

    /// <summary>
    /// Stores the outcome of the request. A storage failure is logged and does not change the result returned to the buyer.
    /// </summary>
    protected virtual async Task SaveOrderRequestAsync(PunchoutOrderRequest orderRequest, string status, PunchoutOrderResult result)
    {
        // The payload id is required and unique, a document without a valid one cannot be stored
        if (string.IsNullOrEmpty(orderRequest.PayloadId) || orderRequest.PayloadId.Length > PayloadIdMaxLength)
        {
            return;
        }

        orderRequest.Status = status;
        orderRequest.CustomerOrderId = result.CustomerOrderId;
        orderRequest.ErrorCode = Truncate(result.Status == PunchoutOrderStatus.Success ? null : result.Status, ErrorCodeMaxLength);
        orderRequest.ErrorMessage = Truncate(result.ErrorMessage, ErrorMessageMaxLength);

        try
        {
            await orderRequestService.SaveChangesAsync([orderRequest]);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Punchout order request '{PurchaseOrderNumber}' with payload '{PayloadId}' cannot be stored with status '{Status}', customer order '{CustomerOrderId}'.",
                orderRequest.PurchaseOrderNumber, orderRequest.PayloadId, status, result.CustomerOrderId);
        }
    }

    protected virtual async Task<PunchoutOrderRequest> FindRequestByPayloadIdAsync(string configurationId, string payloadId)
    {
        if (string.IsNullOrEmpty(payloadId))
        {
            return null;
        }

        var criteria = AbstractTypeFactory<PunchoutOrderRequestSearchCriteria>.TryCreateInstance();
        criteria.ConfigurationId = configurationId;
        criteria.PayloadId = payloadId;
        criteria.Take = 1;

        var searchResult = await orderRequestSearchService.SearchNoCloneAsync(criteria);

        return searchResult.Results.FirstOrDefault();
    }

    protected virtual string GetValidationErrorMessage(ValidationException exception)
    {
        var messages = exception.Errors?
            .Select(x => x.ErrorMessage)
            .Where(x => !string.IsNullOrEmpty(x))
            .Distinct()
            .ToList();

        return messages.IsNullOrEmpty()
            ? exception.Message
            : string.Join(" ", messages);
    }

    /// <summary>
    /// Finds a stored request for the same cXML document, or for the same purchase order sent in another document.
    /// </summary>
    protected virtual async Task<PunchoutOrderRequest> FindExistingRequestAsync(PunchoutOrderRequestData request, PunchoutConfiguration configuration)
    {
        // The same document sent again, e.g. the buyer did not receive the response
        var existingRequest = await FindRequestByPayloadIdAsync(configuration.Id, request.PayloadId);

        if (existingRequest is not null)
        {
            return existingRequest;
        }

        // The same purchase order resent with a new payloadID. Failed and rejected attempts do not block it.
        // Only a new order can duplicate a new order, and an empty order number would match every request.
        if (string.IsNullOrEmpty(request.OrderId) || !IsOrderTypeSupported(request.OrderType))
        {
            return null;
        }

        var criteria = AbstractTypeFactory<PunchoutOrderRequestSearchCriteria>.TryCreateInstance();
        criteria.ConfigurationId = configuration.Id;
        criteria.PurchaseOrderNumber = request.OrderId;
        criteria.OrderType = CxmlConstants.OrderRequestType.New;
        criteria.Statuses = [PunchoutOrderRequestStatus.Completed, PunchoutOrderRequestStatus.Processing];
        criteria.Take = 1;

        var searchResult = await orderRequestSearchService.SearchNoCloneAsync(criteria);

        return searchResult.Results.FirstOrDefault();
    }

    /// <summary>
    /// Returns the result for a request that was already received, or null when it has to be processed again.
    /// </summary>
    protected virtual PunchoutOrderResult GetExistingRequestResult(PunchoutOrderRequestData request, PunchoutOrderRequest existingRequest)
    {
        switch (existingRequest.Status)
        {
            case PunchoutOrderRequestStatus.Completed:
                logger.LogInformation("Punchout order request '{OrderId}' with payload '{PayloadId}' is already processed by request '{ExistingRequestId}', returning the stored result.",
                    request.OrderId, request.PayloadId, existingRequest.Id);

                return PunchoutOrderResult.Success(existingRequest.CustomerOrderId, existingRequest.PurchaseOrderNumber);

            case PunchoutOrderRequestStatus.Processing:
                logger.LogWarning("Punchout order request '{OrderId}' with payload '{PayloadId}' is being processed by request '{ExistingRequestId}'.",
                    request.OrderId, request.PayloadId, existingRequest.Id);

                return PunchoutOrderResult.Error(PunchoutOrderStatus.OrderInProgress, "The order is being processed, please retry later.");

            default:
                return null;
        }
    }

    protected virtual async Task<PunchoutOrderValidationResult> ValidateAsync(PunchoutOrderRequestData request)
    {
        if (Options.Configurations.IsNullOrEmpty())
        {
            logger.LogError("Punchout is not configured: the '{Section}' configuration section has no {Configurations}.",
                ModuleConstants.ConfigurationSections.ConfigurationKey,
                nameof(Options.Configurations));

            return PunchoutOrderValidationResult.Invalid(PunchoutOrderStatus.InvalidCredentials);
        }

        var settings = FindConfiguration(request);

        if (settings is null)
        {
            logger.LogWarning("Punchout order request rejected for sender identity '{SenderIdentity}': no configuration matches the shared secret.",
                request.Sender);

            return PunchoutOrderValidationResult.Invalid(PunchoutOrderStatus.InvalidCredentials);
        }

        if (string.IsNullOrEmpty(settings.Id) || string.IsNullOrEmpty(settings.StoreId))
        {
            logger.LogError("Punchout is not configured: the configuration matched for sender identity '{SenderIdentity}' has no {Missing}.",
                request.Sender,
                string.IsNullOrEmpty(settings.Id) ? nameof(settings.Id) : nameof(settings.StoreId));

            return PunchoutOrderValidationResult.Invalid(PunchoutOrderStatus.StoreNotConfigured);
        }

        if (!AreCredentialsValid(request, settings))
        {
            logger.LogWarning("Punchout order request rejected for sender identity '{SenderIdentity}': invalid credentials.",
                request.Sender);

            return PunchoutOrderValidationResult.Invalid(PunchoutOrderStatus.InvalidCredentials);
        }

        var userMapping = await FindUserMappingAsync(request.Sender);

        if (userMapping is null)
        {
            logger.LogWarning("Punchout order request rejected: sender identity '{SenderIdentity}' is not linked to any user.",
                request.Sender);

            return PunchoutOrderValidationResult.Invalid(PunchoutOrderStatus.UserNotFound, null, settings);
        }

        var store = await storeService.GetPunchoutStoreAsync(settings.StoreId);

        if (store is null)
        {
            logger.LogWarning("Punchout order request rejected for sender identity '{SenderIdentity}': store '{StoreId}' does not exist or punchout is disabled for it.",
                request.Sender, settings.StoreId);

            return PunchoutOrderValidationResult.Invalid(PunchoutOrderStatus.StoreNotConfigured, null, settings, userMapping);
        }

        if (!IsOrderTypeSupported(request.OrderType))
        {
            logger.LogWarning("Punchout order request '{OrderId}' rejected for sender identity '{SenderIdentity}': order type '{OrderType}' is not supported.",
                request.OrderId, request.Sender, request.OrderType);

            return PunchoutOrderValidationResult.Invalid(PunchoutOrderStatus.UnsupportedOrderType,
                $"Order type '{request.OrderType}' is not supported.", settings, userMapping, store);
        }

        var errorMessage = ValidateOrder(request);

        if (errorMessage is not null)
        {
            logger.LogWarning("Punchout order request '{OrderId}' rejected for sender identity '{SenderIdentity}': {Message}",
                request.OrderId, request.Sender, errorMessage);

            return PunchoutOrderValidationResult.Invalid(PunchoutOrderStatus.InvalidRequest, errorMessage, settings, userMapping, store);
        }

        if (!IsCurrencySupported(request.Currency, store))
        {
            logger.LogWarning("Punchout order request '{OrderId}' rejected for sender identity '{SenderIdentity}': currency '{Currency}' is not available in store '{StoreId}'.",
                request.OrderId, request.Sender, request.Currency, store.Id);

            return PunchoutOrderValidationResult.Invalid(PunchoutOrderStatus.CurrencyNotSupported,
                $"Currency '{request.Currency}' is not supported.", settings, userMapping, store);
        }

        return PunchoutOrderValidationResult.Valid(settings, userMapping, store);
    }

    protected virtual bool IsOrderTypeSupported(string orderType)
    {
        // The type attribute defaults to 'new' in the cXML DTD
        return string.IsNullOrEmpty(orderType) || orderType.EqualsIgnoreCase(CxmlConstants.OrderRequestType.New);
    }

    /// <summary>
    /// Validates the order content. Returns the error message returned to the buyer, or null when the order is valid.
    /// </summary>
    protected virtual string ValidateOrder(PunchoutOrderRequestData request)
    {
        if (string.IsNullOrEmpty(request.PayloadId))
        {
            return "The cXML document has no payloadID.";
        }

        if (request.PayloadId.Length > PayloadIdMaxLength)
        {
            return $"The payloadID is longer than {PayloadIdMaxLength} characters.";
        }

        if (string.IsNullOrEmpty(request.OrderId))
        {
            return "The order has no orderID.";
        }

        if (request.OrderId.Length > PurchaseOrderNumberMaxLength)
        {
            return $"The orderID is longer than {PurchaseOrderNumberMaxLength} characters.";
        }

        if (string.IsNullOrEmpty(request.Currency))
        {
            return "The order has no Total currency.";
        }

        if (request.Items.IsNullOrEmpty())
        {
            return "The order has no items.";
        }

        return request.Items
            .Select(x => ValidateItem(request, x))
            .FirstOrDefault(x => x is not null);
    }

    /// <summary>
    /// Validates a single order line. Returns the error message returned to the buyer, or null when the line is valid.
    /// </summary>
    protected virtual string ValidateItem(PunchoutOrderRequestData request, PunchoutOrderRequestItem item)
    {
        if (string.IsNullOrEmpty(item.SupplierPartId))
        {
            return $"Line {item.LineNumber}: SupplierPartID is missing.";
        }

        if (item.Quantity <= 0)
        {
            return $"Line {item.LineNumber}: quantity must be positive.";
        }

        if (item.UnitPrice is null or < 0)
        {
            return $"Line {item.LineNumber}: unit price is missing or invalid.";
        }

        if (!string.IsNullOrEmpty(item.Currency) && !item.Currency.EqualsIgnoreCase(request.Currency))
        {
            return $"Line {item.LineNumber}: currency '{item.Currency}' does not match the order currency '{request.Currency}'.";
        }

        return null;
    }

    protected virtual bool IsCurrencySupported(string currency, Store store)
    {
        return currency.EqualsIgnoreCase(store.DefaultCurrency) ||
               store.Currencies?.Any(x => x.EqualsIgnoreCase(currency)) == true;
    }

    protected virtual PunchoutOrderRequestHandlerContext CreateHandlerContext(
        PunchoutOrderRequestData request,
        PunchoutOrderValidationResult validationResult)
    {
        var context = AbstractTypeFactory<PunchoutOrderRequestHandlerContext>.TryCreateInstance();

        context.Request = request;
        context.Configuration = validationResult.Configuration;
        context.UserMapping = validationResult.UserMapping;
        context.Store = validationResult.Store;

        context.Result = AbstractTypeFactory<PunchoutOrderResult>.TryCreateInstance();

        return context;
    }

    private static string Truncate(string value, int maxLength)
    {
        return value?.Length > maxLength ? value[..maxLength] : value;
    }
}
