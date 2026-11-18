using System;
using System.Collections.Generic;
using System.Globalization;
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
    IPunchoutSessionService sessionService,
    IPunchoutSessionSearchService sessionSearchService,
    IPunchoutOrderMessageSearchService orderMessageSearchService,
    IPunchoutOrderLineService orderLineService,
    AbstractValidator<PunchoutOrderRequestData> requestValidator,
    ILogger<PunchoutOrderRequestProcessor> logger)
    : BasePunchoutProcessor(options, userMappingSearchService), IPunchoutOrderRequestProcessor
{
    protected const string PriceFormat = "0.00";

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

        var addedLines = await AddOrderLinesAsync(request, orderRequest);

        if (addedLines is null)
        {
            logger.LogWarning("Punchout order request '{OrderId}' with payload '{PayloadId}' rejected: a requisition item is ordered by a concurrent purchase order.",
                request.OrderId, request.PayloadId);

            var linesError = PunchoutOrderResult.Error(PunchoutOrderStatus.InvalidRequest, "A requisition item of the order is already ordered by another purchase order.");
            await SaveOrderRequestAsync(orderRequest, PunchoutOrderRequestStatus.Rejected, linesError);

            return linesError;
        }

        var result = await HandleOrderAsync(request, validationResult);

        await UpdateOrderLinesAsync(request, orderRequest, result, addedLines);
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
        // A rejected request is stored with the values it was rejected for, cut to the column sizes.
        orderRequest.PayloadId = request.PayloadId;
        orderRequest.SenderIdentity = Truncate(request.Sender, ModuleConstants.FieldLength.Identity);
        orderRequest.BuyerIdentity = Truncate(request.From, ModuleConstants.FieldLength.Identity);
        orderRequest.UserId = validationResult.UserMapping?.UserId;
        orderRequest.OrderType = Truncate(request.OrderType.IsNullOrEmpty() ? CxmlConstants.OrderRequestType.New : request.OrderType, ModuleConstants.FieldLength.OrderType); // 'type' defaults to 'new' in the cXML DTD
        orderRequest.OrderVersion = Truncate(request.OrderVersion, ModuleConstants.FieldLength.OrderVersion);
        orderRequest.PreviousPayloadId = Truncate(request.PreviousPayloadId, ModuleConstants.FieldLength.PayloadId);
        orderRequest.Cxml = request.Cxml;
        orderRequest.PurchaseOrderNumber = Truncate(request.OrderId, ModuleConstants.FieldLength.PurchaseOrderNumber);
        orderRequest.PurchaseOrderDate = request.OrderDate?.UtcDateTime;
        orderRequest.Currency = Truncate(request.Currency, ModuleConstants.FieldLength.Currency);
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
        // The payload id is required and unique, a document without a valid one cannot be stored.
        // A cut payload id could match another document.
        if (orderRequest.PayloadId.IsNullOrEmpty() || orderRequest.PayloadId.Length > ModuleConstants.FieldLength.PayloadId)
        {
            return;
        }

        orderRequest.Status = status;
        orderRequest.CustomerOrderId = result.CustomerOrderId;
        orderRequest.ErrorCode = Truncate(result.Status == PunchoutOrderStatus.Success ? null : result.Status, ModuleConstants.FieldLength.ErrorCode);
        // The handler may join many line errors, the message must fit or the request stays in Processing
        orderRequest.ErrorMessage = Truncate(result.ErrorMessage, ModuleConstants.FieldLength.ErrorMessage);

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
        if (string.IsNullOrEmpty(request.OrderId) || !IsNewOrder(request.OrderType))
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

        // The content is validated only for an authenticated sender, the rejected request is stored
        var requestValidationResult = await requestValidator.ValidateAsync(request);

        if (!requestValidationResult.IsValid)
        {
            var validationError = requestValidationResult.Errors.First().ErrorMessage;

            logger.LogWarning("Punchout order request '{OrderId}' rejected for sender identity '{SenderIdentity}': {Message}",
                request.OrderId, request.Sender, validationError);

            return PunchoutOrderValidationResult.Invalid(PunchoutOrderStatus.InvalidRequest, validationError, settings, userMapping, store);
        }

        if (!IsCurrencySupported(request.Currency, store))
        {
            logger.LogWarning("Punchout order request '{OrderId}' rejected for sender identity '{SenderIdentity}': currency '{Currency}' is not available in store '{StoreId}'.",
                request.OrderId, request.Sender, request.Currency, store.Id);

            return PunchoutOrderValidationResult.Invalid(PunchoutOrderStatus.CurrencyNotSupported,
                $"Currency '{request.Currency}' is not supported.", settings, userMapping, store);
        }

        // An update or delete changes the customer order created by an earlier request
        if (IsUpdateOrder(request.OrderType) || IsDeleteOrder(request.OrderType))
        {
            return await ValidateUpdateAsync(request, settings, userMapping, store);
        }

        return await ValidateNewOrderAsync(request, settings, userMapping, store);
    }

    /// <summary>
    /// Every order line must be a requisition item returned by a punchout session of the user and not ordered by another purchase order.
    /// </summary>
    protected virtual async Task<PunchoutOrderValidationResult> ValidateNewOrderAsync(PunchoutOrderRequestData request, PunchoutConfiguration settings,
        PunchoutUserMapping userMapping, Store store)
    {
        var errorMessage = await ValidateRequisitionItemsAsync(request, settings, userMapping, isNewOrder: true);

        if (errorMessage is not null)
        {
            logger.LogWarning("Punchout order request '{OrderId}' rejected for sender identity '{SenderIdentity}': {Message}",
                request.OrderId, request.Sender, errorMessage);

            return PunchoutOrderValidationResult.Invalid(PunchoutOrderStatus.InvalidRequest, errorMessage, settings, userMapping, store);
        }

        return PunchoutOrderValidationResult.Valid(settings, userMapping, store);
    }

    /// <summary>
    /// Finds the current version of the purchase order and checks that the update or delete is newer than it.
    /// </summary>
    protected virtual async Task<PunchoutOrderValidationResult> ValidateUpdateAsync(PunchoutOrderRequestData request, PunchoutConfiguration settings,
        PunchoutUserMapping userMapping, Store store)
    {
        // Two versions applied at the same time would overwrite each other
        if (await IsOrderInProgressAsync(request, settings))
        {
            logger.LogWarning("Punchout order request '{OrderId}' with payload '{PayloadId}' rejected: another version of the order is being processed.",
                request.OrderId, request.PayloadId);

            return PunchoutOrderValidationResult.Invalid(PunchoutOrderStatus.OrderInProgress,
                "Another version of the order is being processed, please retry later.", settings, userMapping, store);
        }

        var errorMessage = await ValidateDocumentReferenceAsync(request, settings);

        if (errorMessage is not null)
        {
            logger.LogWarning("Punchout order request '{OrderId}' with payload '{PayloadId}' rejected: {Message}",
                request.OrderId, request.PayloadId, errorMessage);

            return PunchoutOrderValidationResult.Invalid(PunchoutOrderStatus.InvalidRequest, errorMessage, settings, userMapping, store);
        }

        var currentRequest = await FindCurrentOrderVersionAsync(request, settings);

        // The 'new' version was rejected or failed, so no customer order exists and the buyer will not send 'new' again.
        // The update is then created from the requisition, its items still reference the punchout session.
        if (currentRequest is null && IsUpdateOrder(request.OrderType))
        {
            logger.LogInformation("Punchout order request '{OrderId}' with payload '{PayloadId}': no version of the order is processed, the update is created as a new order.",
                request.OrderId, request.PayloadId);

            return await ValidateNewOrderAsync(request, settings, userMapping, store);
        }

        // The buyer cancels a purchase order that never became a customer order, there is nothing to cancel.
        // The handler accepts it without a previous request, so the buyer does not get an error for the cancellation.
        if (string.IsNullOrEmpty(currentRequest?.CustomerOrderId) && IsDeleteOrder(request.OrderType))
        {
            logger.LogInformation("Punchout order request '{OrderId}' with payload '{PayloadId}': no customer order exists for the purchase order, the delete is accepted without changes.",
                request.OrderId, request.PayloadId);

            return PunchoutOrderValidationResult.ValidUpdate(settings, userMapping, store, null);
        }

        if (string.IsNullOrEmpty(currentRequest?.CustomerOrderId))
        {
            logger.LogWarning("Punchout order request '{OrderId}' with payload '{PayloadId}' rejected: the order to change is not found.",
                request.OrderId, request.PayloadId);

            return PunchoutOrderValidationResult.Invalid(PunchoutOrderStatus.InvalidRequest,
                $"Order '{request.OrderId}' is not found.", settings, userMapping, store);
        }

        errorMessage = ValidateOrderVersion(request, currentRequest);

        if (errorMessage is not null)
        {
            logger.LogWarning("Punchout order request '{OrderId}' with payload '{PayloadId}' rejected, current version is stored by request '{CurrentRequestId}': {Message}",
                request.OrderId, request.PayloadId, currentRequest.Id, errorMessage);

            return PunchoutOrderValidationResult.Invalid(PunchoutOrderStatus.InvalidRequest, errorMessage, settings, userMapping, store);
        }

        // The lines added by the update are requisition items, as in a new order
        if (IsUpdateOrder(request.OrderType))
        {
            errorMessage = await ValidateRequisitionItemsAsync(request, settings, userMapping, isNewOrder: false);

            if (errorMessage is not null)
            {
                logger.LogWarning("Punchout order request '{OrderId}' with payload '{PayloadId}' rejected: {Message}",
                    request.OrderId, request.PayloadId, errorMessage);

                return PunchoutOrderValidationResult.Invalid(PunchoutOrderStatus.InvalidRequest, errorMessage, settings, userMapping, store);
            }
        }

        return PunchoutOrderValidationResult.ValidUpdate(settings, userMapping, store, currentRequest);
    }

    /// <summary>
    /// The DocumentReference may point to any earlier version, even a rejected one: the buyer chains the versions it sent.
    /// It must not point to another purchase order. Returns the error message or null.
    /// </summary>
    protected virtual async Task<string> ValidateDocumentReferenceAsync(PunchoutOrderRequestData request, PunchoutConfiguration configuration)
    {
        if (string.IsNullOrEmpty(request.PreviousPayloadId))
        {
            return null;
        }

        if (request.PreviousPayloadId == request.PayloadId)
        {
            return "The DocumentReference points to the document itself.";
        }

        var referencedRequest = await FindRequestByPayloadIdAsync(configuration.Id, request.PreviousPayloadId);

        if (referencedRequest is null)
        {
            // The previous version may have been sent before the order requests were stored, the order number is enough
            logger.LogInformation("Punchout order request '{OrderId}' with payload '{PayloadId}' references unknown payload '{PreviousPayloadId}'.",
                request.OrderId, request.PayloadId, request.PreviousPayloadId);

            return null;
        }

        return referencedRequest.PurchaseOrderNumber == request.OrderId
            ? null
            : $"The DocumentReference '{request.PreviousPayloadId}' belongs to order '{referencedRequest.PurchaseOrderNumber}'.";
    }

    /// <summary>
    /// The last successfully processed version of the purchase order, it references the customer order.
    /// </summary>
    protected virtual async Task<PunchoutOrderRequest> FindCurrentOrderVersionAsync(PunchoutOrderRequestData request, PunchoutConfiguration configuration)
    {
        var criteria = AbstractTypeFactory<PunchoutOrderRequestSearchCriteria>.TryCreateInstance();
        criteria.ConfigurationId = configuration.Id;
        criteria.PurchaseOrderNumber = request.OrderId;
        criteria.Statuses = [PunchoutOrderRequestStatus.Completed];
        criteria.Sort = $"{nameof(PunchoutOrderRequest.CreatedDate)}:desc;{nameof(PunchoutOrderRequest.Id)}:desc";
        criteria.Take = 1;

        var searchResult = await orderRequestSearchService.SearchNoCloneAsync(criteria);

        return searchResult.Results.FirstOrDefault();
    }

    /// <summary>
    /// Another document of the same purchase order is being processed.
    /// </summary>
    protected virtual async Task<bool> IsOrderInProgressAsync(PunchoutOrderRequestData request, PunchoutConfiguration configuration)
    {
        var criteria = AbstractTypeFactory<PunchoutOrderRequestSearchCriteria>.TryCreateInstance();
        criteria.ConfigurationId = configuration.Id;
        criteria.PurchaseOrderNumber = request.OrderId;
        criteria.Statuses = [PunchoutOrderRequestStatus.Processing];
        criteria.Take = 2;

        var searchResult = await orderRequestSearchService.SearchNoCloneAsync(criteria);

        return searchResult.Results.Any(x => x.PayloadId != request.PayloadId);
    }

    /// <summary>
    /// orderVersion is optional in cXML. When both versions are numbers, the update must be newer than the current version,
    /// so a delayed or resent old version does not overwrite a newer one. Returns the error message or null.
    /// </summary>
    protected virtual string ValidateOrderVersion(PunchoutOrderRequestData request, PunchoutOrderRequest currentRequest)
    {
        if (string.IsNullOrEmpty(request.OrderVersion))
        {
            return null;
        }

        if (!int.TryParse(request.OrderVersion, NumberStyles.None, CultureInfo.InvariantCulture, out var version))
        {
            return $"The orderVersion '{request.OrderVersion}' is not a number.";
        }

        // A new order without orderVersion is version 1
        var currentVersion = 1;

        if (!string.IsNullOrEmpty(currentRequest.OrderVersion) &&
            !int.TryParse(currentRequest.OrderVersion, NumberStyles.None, CultureInfo.InvariantCulture, out currentVersion))
        {
            return null;
        }

        return version > currentVersion
            ? null
            : $"The orderVersion {version} is not newer than the current version {currentVersion}.";
    }

    /// <summary>
    /// The session of an item from its SupplierPartAuxiliaryID, or the last returned session of the user
    /// when the buyer returned the line item id without the session id.
    /// </summary>
    protected virtual async Task<PunchoutSession> GetSessionAsync(string sessionId, PunchoutConfiguration settings, PunchoutUserMapping userMapping)
    {
        return !string.IsNullOrEmpty(sessionId)
            ? await GetReturnedSessionAsync(sessionId, settings, userMapping)
            : await FindReturnedSessionAsync(settings, userMapping);
    }

    /// <summary>
    /// Returns the session if it returned a requisition for the user and the configuration, otherwise null.
    /// </summary>
    protected virtual async Task<PunchoutSession> GetReturnedSessionAsync(string sessionId, PunchoutConfiguration configuration, PunchoutUserMapping userMapping)
    {
        var session = await sessionService.GetNoCloneAsync(sessionId);

        var isReturnedSession = session != null &&
            session.Status == ModuleConstants.SessionStatus.Returned &&
            session.StoreId.EqualsIgnoreCase(configuration.StoreId) &&
            session.ConfigurationId.EqualsIgnoreCase(configuration.Id) &&
            session.UserId.EqualsIgnoreCase(userMapping.UserId);

        return isReturnedSession ? session : null;
    }

    /// <summary>
    /// Finds the last session of the user that returned a requisition.
    /// </summary>
    protected virtual async Task<PunchoutSession> FindReturnedSessionAsync(PunchoutConfiguration configuration, PunchoutUserMapping userMapping)
    {
        var criteria = AbstractTypeFactory<PunchoutSessionSearchCriteria>.TryCreateInstance();
        criteria.StoreId = configuration.StoreId;
        criteria.ConfigurationId = configuration.Id;
        criteria.UserId = userMapping.UserId;
        criteria.Statuses = [ModuleConstants.SessionStatus.Returned];
        criteria.Sort = $"{nameof(PunchoutSession.ModifiedDate)}:desc;{nameof(PunchoutSession.CreatedDate)}:desc";
        criteria.Take = 1;

        var searchResult = await sessionSearchService.SearchNoCloneAsync(criteria);

        return searchResult.Results.FirstOrDefault();
    }

    /// <summary>
    /// Matches every order line with the requisition item it orders and sets <see cref="PunchoutOrderRequestItem.RequisitionItem"/>.
    /// The items of one purchase order may come from several sessions, each line references its session in the SupplierPartAuxiliaryID.
    /// A requisition item ordered by another purchase order is rejected. A line of an 'update' that is already ordered by the purchase order
    /// is not matched, the handler compares it with the customer order.
    /// Returns the error message or null when the order matches the requisitions.
    /// </summary>
    protected virtual async Task<string> ValidateRequisitionItemsAsync(PunchoutOrderRequestData request, PunchoutConfiguration configuration,
        PunchoutUserMapping userMapping, bool isNewOrder)
    {
        var errors = new List<string>();
        var references = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in request.Items)
        {
            if (string.IsNullOrEmpty(item.LineItemId))
            {
                errors.Add($"Line {item.LineNumber}: SupplierPartAuxiliaryID is missing.");
            }
            else if (!references.Add(PunchoutItemReference.Format(item.LineItemId, item.SessionId)))
            {
                errors.Add($"Line {item.LineNumber}: SupplierPartAuxiliaryID '{item.SupplierPartAuxiliaryId}' is used by another line.");
            }
        }

        if (errors.Count > 0)
        {
            return string.Join(" ", errors);
        }

        var lineItemIds = request.Items.Select(x => x.LineItemId).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var orderedLines = await orderLineService.GetByLineItemIdsAsync(configuration.Id, lineItemIds);
        var requisitions = new Dictionary<string, (PunchoutSession Session, IList<PunchoutRequisitionItem> Items)>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in request.Items)
        {
            item.RequisitionItem = null;

            var orderedLine = orderedLines.FirstOrDefault(x => IsSameLine(x, item));

            if (orderedLine is not null && !orderedLine.PurchaseOrderNumber.EqualsIgnoreCase(request.OrderId))
            {
                errors.Add($"Line {item.LineNumber}: item '{item.SupplierPartId}' is already ordered by purchase order '{orderedLine.PurchaseOrderNumber}'.");
                continue;
            }

            if (orderedLine is not null && !isNewOrder)
            {
                continue;
            }

            var sessionKey = item.SessionId ?? string.Empty;

            if (!requisitions.TryGetValue(sessionKey, out var requisition))
            {
                var session = await GetSessionAsync(item.SessionId, configuration, userMapping);
                requisition = (session, session is null ? [] : await GetRequisitionItemsAsync(session));
                requisitions[sessionKey] = requisition;
            }

            if (requisition.Session is null)
            {
                errors.Add($"Line {item.LineNumber}: no punchout session '{item.SessionId}' returned a requisition for the user.");
                continue;
            }

            var requisitionItem = requisition.Items.FirstOrDefault(x => x.LineItemId.EqualsIgnoreCase(item.LineItemId));

            var error = requisitionItem is null
                ? $"Line {item.LineNumber}: item '{item.SupplierPartId}' is not in the punchout requisition."
                : ReconcileRequisitionItem(request, item, requisitionItem, isNewOrder);

            if (error is not null)
            {
                errors.Add(error);
                continue;
            }

            item.RequisitionItem = requisitionItem;
        }

        return errors.Count > 0 ? string.Join(" ", errors) : null;
    }

    /// <summary>
    /// Compares an order line with the requisition item sent to the buyer. The quantity of a new order must match,
    /// the handler compares the quantity of a line added by an 'update'. Returns the mismatch or null.
    /// </summary>
    protected virtual string ReconcileRequisitionItem(PunchoutOrderRequestData request, PunchoutOrderRequestItem item, PunchoutRequisitionItem requisitionItem, bool isNewOrder)
    {
        if (!item.SupplierPartId.EqualsIgnoreCase(requisitionItem.Sku))
        {
            return $"Line {item.LineNumber}: SupplierPartID '{item.SupplierPartId}' does not match the requisition item '{requisitionItem.Sku}'.";
        }

        if (!request.Currency.EqualsIgnoreCase(requisitionItem.Currency))
        {
            return $"Line {item.LineNumber}: the order currency '{request.Currency}' does not match the requisition currency '{requisitionItem.Currency}'.";
        }

        if (isNewOrder && item.Quantity != requisitionItem.Quantity)
        {
            return $"Line {item.LineNumber}: quantity {item.Quantity.ToString(CultureInfo.InvariantCulture)} does not match the requisition quantity {requisitionItem.Quantity}.";
        }

        if (item.UnitPrice is null || RoundPrice(item.UnitPrice.Value) != RoundPrice(requisitionItem.PlacedPrice))
        {
            return $"Line {item.LineNumber}: unit price {item.UnitPrice?.ToString(PriceFormat, CultureInfo.InvariantCulture)} does not match the requisition price {requisitionItem.PlacedPrice.ToString(PriceFormat, CultureInfo.InvariantCulture)}.";
        }

        return null;
    }

    /// <summary>
    /// The order message sends the prices with two decimals.
    /// </summary>
    protected virtual decimal RoundPrice(decimal price)
    {
        return Math.Round(price, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// The buyer may return the line item id without the session id, then the line item id is compared only.
    /// </summary>
    protected virtual bool IsSameLine(PunchoutOrderLine line, PunchoutOrderRequestItem item)
    {
        return line.LineItemId.EqualsIgnoreCase(item.LineItemId) &&
               (string.IsNullOrEmpty(item.SessionId) || line.SessionId.EqualsIgnoreCase(item.SessionId));
    }

    /// <summary>
    /// The snapshot of the items stored with the PunchOutOrderMessage when the session returned the requisition.
    /// </summary>
    protected virtual async Task<IList<PunchoutRequisitionItem>> GetRequisitionItemsAsync(PunchoutSession session)
    {
        var criteria = AbstractTypeFactory<PunchoutOrderMessageSearchCriteria>.TryCreateInstance();
        criteria.SessionId = session.Id;
        criteria.Take = 1;

        var searchResult = await orderMessageSearchService.SearchNoCloneAsync(criteria);
        var orderMessage = searchResult.Results.FirstOrDefault();

        if (orderMessage is not null && orderMessage.Items.IsNullOrEmpty())
        {
            logger.LogWarning("Punchout order message '{OrderMessageId}' of session '{SessionId}' has no item snapshot, its items cannot be ordered.",
                orderMessage.Id, session.Id);
        }

        return orderMessage?.Items ?? [];
    }

    /// <summary>
    /// Reserves the requisition items for the purchase order before the order is created, so a concurrent purchase order cannot order them.
    /// Returns the added lines, or null if an item is already ordered by another purchase order.
    /// </summary>
    protected virtual async Task<IList<PunchoutOrderLine>> AddOrderLinesAsync(PunchoutOrderRequestData request, PunchoutOrderRequest orderRequest)
    {
        var existingLines = await orderLineService.GetByPurchaseOrderAsync(orderRequest.ConfigurationId, request.OrderId);

        var lines = request.Items
            .Where(x => x.RequisitionItem is not null)
            .Where(x => !existingLines.Any(line => line.SessionId.EqualsIgnoreCase(x.RequisitionItem.SessionId) && line.LineItemId.EqualsIgnoreCase(x.RequisitionItem.LineItemId)))
            .Select(x => CreateOrderLine(request, orderRequest, x))
            .ToList();

        return await orderLineService.TryAddAsync(lines) ? lines : null;
    }

    protected virtual PunchoutOrderLine CreateOrderLine(PunchoutOrderRequestData request, PunchoutOrderRequest orderRequest, PunchoutOrderRequestItem item)
    {
        var line = AbstractTypeFactory<PunchoutOrderLine>.TryCreateInstance();

        line.ConfigurationId = orderRequest.ConfigurationId;
        line.SessionId = item.RequisitionItem.SessionId;
        line.LineItemId = item.RequisitionItem.LineItemId;
        line.PurchaseOrderNumber = request.OrderId;
        line.LineNumber = Truncate(item.LineNumber, ModuleConstants.FieldLength.LineNumber);
        line.OrderRequestId = orderRequest.Id;

        return line;
    }

    /// <summary>
    /// A failed request releases the items it reserved. A processed update releases the items removed from the purchase order,
    /// a processed delete releases all the items of the purchase order. A failure here is logged and does not change the result.
    /// </summary>
    protected virtual async Task UpdateOrderLinesAsync(PunchoutOrderRequestData request, PunchoutOrderRequest orderRequest, PunchoutOrderResult result, IList<PunchoutOrderLine> addedLines)
    {
        try
        {
            if (result.Status != PunchoutOrderStatus.Success)
            {
                await orderLineService.DeleteAsync(addedLines.Select(x => x.Id).ToList());
                return;
            }

            if (IsNewOrder(request.OrderType))
            {
                return;
            }

            var lines = await orderLineService.GetByPurchaseOrderAsync(orderRequest.ConfigurationId, request.OrderId);

            var removedLines = IsDeleteOrder(request.OrderType)
                ? lines
                : lines.Where(line => !request.Items.Any(item => IsSameLine(line, item))).ToList();

            await orderLineService.DeleteAsync(removedLines.Select(x => x.Id).ToList());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Punchout order lines of purchase order '{PurchaseOrderNumber}' cannot be updated after request '{OrderRequestId}'.",
                request.OrderId, orderRequest.Id);
        }
    }

    protected virtual bool IsOrderTypeSupported(string orderType)
    {
        return IsNewOrder(orderType) || IsUpdateOrder(orderType) || IsDeleteOrder(orderType);
    }

    protected virtual bool IsNewOrder(string orderType)
    {
        // The type attribute defaults to 'new' in the cXML DTD
        return string.IsNullOrEmpty(orderType) || orderType.EqualsIgnoreCase(CxmlConstants.OrderRequestType.New);
    }

    protected virtual bool IsUpdateOrder(string orderType)
    {
        return orderType.EqualsIgnoreCase(CxmlConstants.OrderRequestType.Update);
    }

    protected virtual bool IsDeleteOrder(string orderType)
    {
        return orderType.EqualsIgnoreCase(CxmlConstants.OrderRequestType.Delete);
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
        context.PreviousRequest = validationResult.PreviousRequest;

        context.Result = AbstractTypeFactory<PunchoutOrderResult>.TryCreateInstance();

        return context;
    }

    protected static string Truncate(string value, int maxLength)
    {
        return value?.Length > maxLength ? value[..maxLength] : value;
    }
}
