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

public partial class PunchoutOrderRequestProcessor(
    IOptions<PunchoutOptions> options,
    IPunchoutUserMappingSearchService userMappingSearchService,
    IStoreService storeService,
    IPunchoutOrderRequestService orderRequestService,
    IPunchoutOrderRequestSearchService orderRequestSearchService,
    IPunchoutHandlerFactory handlerFactory,
    AbstractValidator<PunchoutOrderRequestData> requestValidator,
    IPunchoutOrganizationUserResolver organizationUserResolver,
    ILogger<PunchoutOrderRequestProcessor> logger)
    : BasePunchoutProcessor(options, userMappingSearchService), IPunchoutOrderRequestProcessor
{
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

        var result = await HandleOrderAsync(request, validationResult, orderRequest);

        await SaveOrderRequestAsync(orderRequest, GetOrderRequestStatus(result), result);

        return result;
    }

    protected virtual async Task<PunchoutOrderResult> HandleOrderAsync(PunchoutOrderRequestData request, PunchoutOrderValidationResult validationResult,
        PunchoutOrderRequest orderRequest)
    {
        var handler = handlerFactory.Create(validationResult.Configuration);
        if (handler == null)
        {
            return PunchoutOrderResult.Error(PunchoutOrderStatus.ConfigurationError);
        }

        var handlerContext = CreateHandlerContext(request, validationResult, orderRequest);
        try
        {
            await handler.HandleOrderRequestAsync(handlerContext);
        }
        catch (Exception ex)
        {
            LogHandlerException(ex, handler.GetType().Name, request.OrderId);

            return PunchoutOrderResult.Error(PunchoutOrderStatus.OrderError);
        }

        if (handlerContext.IsFailed)
        {
            LogHandlerFailed(handler.GetType().Name, request.OrderId, handlerContext.ErrorCode, handlerContext.ErrorMessage);

            return PunchoutOrderResult.Error(handlerContext.ErrorCode, handlerContext.ErrorMessage);
        }

        return PunchoutOrderResult.Success(handlerContext.Result.CustomerOrderId, handlerContext.Result.PurchaseOrderNumber);
    }

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
    /// A failed or rejected attempt of the same document reuses its record: the payload id is unique within the configuration.
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
        // Truncated to the column sizes, a rejected request is stored too
        orderRequest.PayloadId = request.PayloadId;
        orderRequest.SenderIdentity = Truncate(request.Sender, ModuleConstants.FieldLength.Identity);
        orderRequest.BuyerIdentity = Truncate(request.From, ModuleConstants.FieldLength.Identity);
        orderRequest.UserId = validationResult.Customer?.UserId ?? validationResult.UserMapping?.UserId;
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
    /// Returns null when the processing can start, or the result of a concurrent request for the same document.
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
            // The unique payload id index rejects the same document inserted concurrently
            var concurrentRequest = await FindRequestByPayloadIdAsync(orderRequest.ConfigurationId, orderRequest.PayloadId);

            if (concurrentRequest is null || concurrentRequest.Id == orderRequest.Id)
            {
                throw;
            }

            LogStoredByConcurrentRequest(ex, request.OrderId, request.PayloadId, concurrentRequest.Id);

            return GetExistingRequestResult(request, concurrentRequest)
                ?? PunchoutOrderResult.Error(PunchoutOrderStatus.OrderInProgress, "The order is being processed, please retry later.");
        }
    }

    /// <summary>
    /// A storage failure is logged and does not change the result returned to the buyer.
    /// </summary>
    protected virtual async Task SaveOrderRequestAsync(PunchoutOrderRequest orderRequest, string status, PunchoutOrderResult result)
    {
        // Not stored without a valid payload id: a truncated one could match another document
        if (orderRequest.PayloadId.IsNullOrEmpty() || orderRequest.PayloadId.Length > ModuleConstants.FieldLength.PayloadId)
        {
            return;
        }

        orderRequest.Status = status;
        orderRequest.CustomerOrderId = result.CustomerOrderId;
        orderRequest.ErrorCode = Truncate(result.Status == PunchoutOrderStatus.Success ? null : result.Status, ModuleConstants.FieldLength.ErrorCode);
        // Joined line errors may not fit, and a failed save leaves the request in Processing
        orderRequest.ErrorMessage = Truncate(result.ErrorMessage, ModuleConstants.FieldLength.ErrorMessage);

        try
        {
            await orderRequestService.SaveChangesAsync([orderRequest]);
        }
        catch (Exception ex)
        {
            LogOrderRequestNotStored(ex, orderRequest.PurchaseOrderNumber, orderRequest.PayloadId, status, result.CustomerOrderId);
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
        // The same document resent, e.g. the buyer did not receive the response
        var existingRequest = await FindRequestByPayloadIdAsync(configuration.Id, request.PayloadId);

        if (existingRequest?.Status is PunchoutOrderRequestStatus.Completed or PunchoutOrderRequestStatus.Processing)
        {
            return existingRequest;
        }

        // A resent or delayed 'new' must not create the order again once any version is processed.
        // An empty order number would match every request.
        if (string.IsNullOrEmpty(request.OrderId) || !IsNewOrder(request.OrderType))
        {
            return existingRequest;
        }

        var processedRequest = await FindProcessedOrderVersionAsync(configuration.Id, request.OrderId);

        return processedRequest ?? existingRequest;
    }

    protected virtual async Task<PunchoutOrderRequest> FindProcessedOrderVersionAsync(string configurationId, string purchaseOrderNumber)
    {
        var criteria = AbstractTypeFactory<PunchoutOrderRequestSearchCriteria>.TryCreateInstance();
        criteria.ConfigurationId = configurationId;
        criteria.PurchaseOrderNumber = purchaseOrderNumber;
        criteria.Statuses = [PunchoutOrderRequestStatus.Completed, PunchoutOrderRequestStatus.Processing];
        criteria.Sort = $"{nameof(PunchoutOrderRequest.CreatedDate)}:desc;{nameof(PunchoutOrderRequest.Id)}:desc";
        criteria.Take = 1;

        var searchResult = await orderRequestSearchService.SearchNoCloneAsync(criteria);

        return searchResult.Results.FirstOrDefault();
    }

    /// <summary>
    /// Returns null when the request has to be processed again.
    /// </summary>
    protected virtual PunchoutOrderResult GetExistingRequestResult(PunchoutOrderRequestData request, PunchoutOrderRequest existingRequest)
    {
        switch (existingRequest.Status)
        {
            case PunchoutOrderRequestStatus.Completed:
                LogAlreadyProcessed(request.OrderId, request.PayloadId, existingRequest.Id);

                return PunchoutOrderResult.Success(existingRequest.CustomerOrderId, existingRequest.PurchaseOrderNumber);

            case PunchoutOrderRequestStatus.Processing:
                LogBeingProcessed(request.OrderId, request.PayloadId, existingRequest.Id);

                return PunchoutOrderResult.Error(PunchoutOrderStatus.OrderInProgress, "The order is being processed, please retry later.");

            default:
                return null;
        }
    }

    protected virtual async Task<PunchoutOrderValidationResult> ValidateAsync(PunchoutOrderRequestData request)
    {
        if (Options.Configurations.IsNullOrEmpty())
        {
            LogNotConfigured(ModuleConstants.ConfigurationSections.ConfigurationKey, nameof(Options.Configurations));

            return PunchoutOrderValidationResult.Invalid(PunchoutOrderStatus.InvalidCredentials);
        }

        var settings = FindConfiguration(request);

        if (settings is null)
        {
            LogConfigurationNotFound(request.Sender);

            return PunchoutOrderValidationResult.Invalid(PunchoutOrderStatus.InvalidCredentials);
        }

        if (string.IsNullOrEmpty(settings.Id) || string.IsNullOrEmpty(settings.StoreId))
        {
            LogConfigurationIncomplete(request.Sender, string.IsNullOrEmpty(settings.Id) ? nameof(settings.Id) : nameof(settings.StoreId));

            return PunchoutOrderValidationResult.Invalid(PunchoutOrderStatus.StoreNotConfigured);
        }

        if (!AreCredentialsValid(request, settings))
        {
            LogInvalidCredentials(request.Sender);

            return PunchoutOrderValidationResult.Invalid(PunchoutOrderStatus.InvalidCredentials);
        }

        var mappingIdentity = GetMappingIdentity(request);
        var userMapping = await FindUserMappingAsync(mappingIdentity);

        if (userMapping is null)
        {
            LogUserNotFound(mappingIdentity);

            return PunchoutOrderValidationResult.Invalid(PunchoutOrderStatus.UserNotFound, null, settings);
        }

        var store = await storeService.GetPunchoutStoreAsync(settings.StoreId);

        if (store is null)
        {
            LogStoreNotConfigured(request.Sender, settings.StoreId);

            return PunchoutOrderValidationResult.Invalid(PunchoutOrderStatus.StoreNotConfigured, null, settings, userMapping);
        }

        if (!IsOrderTypeSupported(request.OrderType))
        {
            LogUnsupportedOrderType(request.OrderId, request.Sender, request.OrderType);

            return PunchoutOrderValidationResult.Invalid(PunchoutOrderStatus.UnsupportedOrderType,
                $"Order type '{request.OrderType}' is not supported.", settings, userMapping, store);
        }

        // The content is validated only for an authenticated sender, the rejected request is stored
        var requestValidationResult = await requestValidator.ValidateAsync(request);

        if (!requestValidationResult.IsValid)
        {
            var validationError = requestValidationResult.Errors.First().ErrorMessage;

            LogInvalidRequest(request.OrderId, request.Sender, validationError);

            return PunchoutOrderValidationResult.Invalid(PunchoutOrderStatus.InvalidRequest, validationError, settings, userMapping, store);
        }

        if (!IsCurrencySupported(request.Currency, store))
        {
            LogCurrencyNotSupported(request.OrderId, request.Sender, request.Currency, store.Id);

            return PunchoutOrderValidationResult.Invalid(PunchoutOrderStatus.CurrencyNotSupported,
                $"Currency '{request.Currency}' is not supported.", settings, userMapping, store);
        }

        // Applies to every order type: the end user must belong to the mapped organization
        var customerResult = await ResolveCustomerAsync(request, settings, userMapping, store);

        if (!customerResult.IsValid)
        {
            return customerResult;
        }

        var result = IsUpdateOrder(request.OrderType) || IsDeleteOrder(request.OrderType)
            ? await ValidateUpdateAsync(request, settings, userMapping, store)
            : await ValidateNewOrderAsync(request, settings, userMapping, store);

        result.Customer ??= customerResult.Customer;

        return result;
    }

    /// <summary>
    /// The customer of a user mapping is the mapped user. The customer of an organization mapping is the end user of the request,
    /// found among the users of the organization by email. Returns either an error or the customer.
    /// </summary>
    protected virtual async Task<PunchoutOrderValidationResult> ResolveCustomerAsync(PunchoutOrderRequestData request, PunchoutConfiguration settings,
        PunchoutUserMapping userMapping, Store store)
    {
        var customer = AbstractTypeFactory<PunchoutOrderCustomer>.TryCreateInstance();

        if (!userMapping.IsOrganizationMapping)
        {
            customer.UserId = userMapping.UserId;
            customer.UserName = userMapping.UserName;
            customer.MemberId = userMapping.MemberId;

            return new PunchoutOrderValidationResult { Customer = customer };
        }

        var email = GetUserEmail(request.User, request.Extrinsics);

        if (string.IsNullOrWhiteSpace(email))
        {
            LogUserEmailMissing(request.OrderId, userMapping.ExternalId, userMapping.OrganizationId);

            return PunchoutOrderValidationResult.Invalid(PunchoutOrderStatus.InvalidRequest,
                "The request contains no end user email.", settings, userMapping, store);
        }

        var userRequest = AbstractTypeFactory<PunchoutOrganizationUserRequest>.TryCreateInstance();
        userRequest.OrganizationId = userMapping.OrganizationId;
        userRequest.Email = email;
        userRequest.StoreId = settings.StoreId;

        var userResult = await organizationUserResolver.ResolveAsync(userRequest);

        switch (userResult.Status)
        {
            case PunchoutOrganizationUserStatus.Found:
                customer.UserId = userResult.User.Id;
                customer.UserName = userResult.User.UserName;
                customer.MemberId = userResult.User.MemberId;
                customer.OrganizationId = userMapping.OrganizationId;

                return new PunchoutOrderValidationResult { Customer = customer };

            case PunchoutOrganizationUserStatus.Ambiguous:
                LogAmbiguousUser(request.OrderId, email, userMapping.OrganizationId, userResult.UserMatchCount);

                return PunchoutOrderValidationResult.Invalid(PunchoutOrderStatus.InvalidRequest,
                    $"Several users of the organization have the end user email '{email}'.", settings, userMapping, store);

            default:
                LogOrganizationUserNotFound(request.OrderId, email, userMapping.OrganizationId);

                return PunchoutOrderValidationResult.Invalid(PunchoutOrderStatus.InvalidRequest,
                    $"The end user '{email}' is not found in the organization.", settings, userMapping, store);
        }
    }

    /// <summary>
    /// The lines are matched with the requisition items by the handler.
    /// </summary>
    protected virtual Task<PunchoutOrderValidationResult> ValidateNewOrderAsync(PunchoutOrderRequestData request, PunchoutConfiguration settings,
        PunchoutUserMapping userMapping, Store store)
    {
        return Task.FromResult(PunchoutOrderValidationResult.Valid(settings, userMapping, store));
    }

    protected virtual async Task<PunchoutOrderValidationResult> ValidateUpdateAsync(PunchoutOrderRequestData request, PunchoutConfiguration settings,
        PunchoutUserMapping userMapping, Store store)
    {
        // Two versions applied at the same time would overwrite each other
        if (await IsOrderInProgressAsync(request, settings))
        {
            LogAnotherVersionInProgress(request.OrderId, request.PayloadId);

            return PunchoutOrderValidationResult.Invalid(PunchoutOrderStatus.OrderInProgress,
                "Another version of the order is being processed, please retry later.", settings, userMapping, store);
        }

        var errorMessage = await ValidateDocumentReferenceAsync(request, settings);

        if (errorMessage is not null)
        {
            LogInvalidDocumentReference(request.OrderId, request.PayloadId, errorMessage);

            return PunchoutOrderValidationResult.Invalid(PunchoutOrderStatus.InvalidRequest, errorMessage, settings, userMapping, store);
        }

        var currentRequest = await FindCurrentOrderVersionAsync(request, settings);

        errorMessage = await ValidateRetriedVersionAsync(request, settings, currentRequest);

        if (errorMessage is not null)
        {
            LogVersionRejected(request.OrderId, request.PayloadId, currentRequest?.Id, errorMessage);

            return PunchoutOrderValidationResult.Invalid(PunchoutOrderStatus.InvalidRequest, errorMessage, settings, userMapping, store);
        }

        // The 'new' failed and will not be resent: create the order from the requisition
        if (currentRequest is null && IsUpdateOrder(request.OrderType))
        {
            LogUpdateCreatedAsNewOrder(request.OrderId, request.PayloadId);

            return await ValidateNewOrderAsync(request, settings, userMapping, store);
        }

        // Nothing to cancel: accepted without a previous request, so the buyer gets no error
        if (string.IsNullOrEmpty(currentRequest?.CustomerOrderId) && IsDeleteOrder(request.OrderType))
        {
            LogDeleteWithoutCustomerOrder(request.OrderId, request.PayloadId);

            return PunchoutOrderValidationResult.ValidUpdate(settings, userMapping, store, null);
        }

        if (string.IsNullOrEmpty(currentRequest?.CustomerOrderId))
        {
            LogOrderToChangeNotFound(request.OrderId, request.PayloadId);

            return PunchoutOrderValidationResult.Invalid(PunchoutOrderStatus.InvalidRequest,
                $"Order '{request.OrderId}' is not found.", settings, userMapping, store);
        }

        errorMessage = ValidateOrderVersion(request, currentRequest);

        if (errorMessage is not null)
        {
            LogVersionRejected(request.OrderId, request.PayloadId, currentRequest.Id, errorMessage);

            return PunchoutOrderValidationResult.Invalid(PunchoutOrderStatus.InvalidRequest, errorMessage, settings, userMapping, store);
        }

        return PunchoutOrderValidationResult.ValidUpdate(settings, userMapping, store, currentRequest);
    }

    /// <summary>
    /// The DocumentReference may point to any earlier version, even a rejected one, but not to another purchase order.
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
            // May predate the stored history, the order number is enough
            LogUnknownDocumentReference(request.OrderId, request.PayloadId, request.PreviousPayloadId);

            return null;
        }

        return referencedRequest.PurchaseOrderNumber == request.OrderId
            ? null
            : $"The DocumentReference '{request.PreviousPayloadId}' belongs to order '{referencedRequest.PurchaseOrderNumber}'.";
    }

    /// <summary>
    /// The latest completed version, it references the customer order.
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
    /// A resent failed or rejected document must not overwrite a version received after it and already processed.
    /// </summary>
    protected virtual async Task<string> ValidateRetriedVersionAsync(PunchoutOrderRequestData request, PunchoutConfiguration configuration, PunchoutOrderRequest currentRequest)
    {
        if (currentRequest is null)
        {
            return null;
        }

        var retriedRequest = await FindRequestByPayloadIdAsync(configuration.Id, request.PayloadId);

        if (retriedRequest is null || retriedRequest.Id == currentRequest.Id || retriedRequest.CreatedDate >= currentRequest.CreatedDate)
        {
            return null;
        }

        return $"A newer version of the order is already processed in document '{currentRequest.PayloadId}'.";
    }

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
    /// orderVersion is optional. When both versions are numbers, the update must be newer than the current one.
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
        PunchoutOrderValidationResult validationResult,
        PunchoutOrderRequest orderRequest)
    {
        var context = AbstractTypeFactory<PunchoutOrderRequestHandlerContext>.TryCreateInstance();

        context.Request = request;
        context.OrderRequestId = orderRequest.Id;
        context.Configuration = validationResult.Configuration;
        context.UserMapping = validationResult.UserMapping;
        context.Customer = validationResult.Customer;
        context.Store = validationResult.Store;
        context.PreviousRequest = validationResult.PreviousRequest;

        context.Result = AbstractTypeFactory<PunchoutOrderResult>.TryCreateInstance();

        return context;
    }

    protected static string Truncate(string value, int maxLength)
    {
        return value?.Length > maxLength ? value[..maxLength] : value;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Punchout '{HandlerName}' handler failed to process order '{PurchaseOrderNumber}'")]
    private partial void LogHandlerException(Exception ex, string handlerName, string purchaseOrderNumber);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Punchout '{HandlerName}' handler failed to process order '{PurchaseOrderNumber}' with status '{Status}': {Message}")]
    private partial void LogHandlerFailed(string handlerName, string purchaseOrderNumber, string status, string message);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Punchout order request '{OrderId}' with payload '{PayloadId}' is already stored by request '{ExistingRequestId}'.")]
    private partial void LogStoredByConcurrentRequest(Exception ex, string orderId, string payloadId, string existingRequestId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Punchout order request '{PurchaseOrderNumber}' with payload '{PayloadId}' cannot be stored with status '{Status}', customer order '{CustomerOrderId}'.")]
    private partial void LogOrderRequestNotStored(Exception ex, string purchaseOrderNumber, string payloadId, string status, string customerOrderId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Punchout order request '{OrderId}' with payload '{PayloadId}' is already processed by request '{ExistingRequestId}', returning the stored result.")]
    private partial void LogAlreadyProcessed(string orderId, string payloadId, string existingRequestId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Punchout order request '{OrderId}' with payload '{PayloadId}' is being processed by request '{ExistingRequestId}'.")]
    private partial void LogBeingProcessed(string orderId, string payloadId, string existingRequestId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Punchout is not configured: the '{Section}' configuration section has no {Configurations}.")]
    private partial void LogNotConfigured(string section, string configurations);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Punchout order request rejected for sender identity '{SenderIdentity}': no configuration matches the shared secret.")]
    private partial void LogConfigurationNotFound(string senderIdentity);

    [LoggerMessage(Level = LogLevel.Error, Message = "Punchout is not configured: the configuration matched for sender identity '{SenderIdentity}' has no {Missing}.")]
    private partial void LogConfigurationIncomplete(string senderIdentity, string missing);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Punchout order request rejected for sender identity '{SenderIdentity}': invalid credentials.")]
    private partial void LogInvalidCredentials(string senderIdentity);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Punchout order request rejected: identity '{MappingIdentity}' is not linked to any user or organization.")]
    private partial void LogUserNotFound(string mappingIdentity);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Punchout order request '{OrderId}' rejected for identity '{MappingIdentity}' mapped to organization '{OrganizationId}': the request contains no end user email.")]
    private partial void LogUserEmailMissing(string orderId, string mappingIdentity, string organizationId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Punchout order request '{OrderId}' rejected: no user with email '{Email}' is found in organization '{OrganizationId}'.")]
    private partial void LogOrganizationUserNotFound(string orderId, string email, string organizationId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Punchout order request '{OrderId}' rejected: {MatchCount} users with email '{Email}' are found in organization '{OrganizationId}'.")]
    private partial void LogAmbiguousUser(string orderId, string email, string organizationId, int matchCount);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Punchout order request rejected for sender identity '{SenderIdentity}': store '{StoreId}' does not exist or punchout is disabled for it.")]
    private partial void LogStoreNotConfigured(string senderIdentity, string storeId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Punchout order request '{OrderId}' rejected for sender identity '{SenderIdentity}': order type '{OrderType}' is not supported.")]
    private partial void LogUnsupportedOrderType(string orderId, string senderIdentity, string orderType);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Punchout order request '{OrderId}' rejected for sender identity '{SenderIdentity}': {Message}")]
    private partial void LogInvalidRequest(string orderId, string senderIdentity, string message);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Punchout order request '{OrderId}' rejected for sender identity '{SenderIdentity}': currency '{Currency}' is not available in store '{StoreId}'.")]
    private partial void LogCurrencyNotSupported(string orderId, string senderIdentity, string currency, string storeId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Punchout order request '{OrderId}' with payload '{PayloadId}' rejected: another version of the order is being processed.")]
    private partial void LogAnotherVersionInProgress(string orderId, string payloadId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Punchout order request '{OrderId}' with payload '{PayloadId}' rejected: {Message}")]
    private partial void LogInvalidDocumentReference(string orderId, string payloadId, string message);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Punchout order request '{OrderId}' with payload '{PayloadId}' rejected, current version is stored by request '{CurrentRequestId}': {Message}")]
    private partial void LogVersionRejected(string orderId, string payloadId, string currentRequestId, string message);

    [LoggerMessage(Level = LogLevel.Information, Message = "Punchout order request '{OrderId}' with payload '{PayloadId}': no version of the order is processed, the update is created as a new order.")]
    private partial void LogUpdateCreatedAsNewOrder(string orderId, string payloadId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Punchout order request '{OrderId}' with payload '{PayloadId}': no customer order exists for the purchase order, the delete is accepted without changes.")]
    private partial void LogDeleteWithoutCustomerOrder(string orderId, string payloadId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Punchout order request '{OrderId}' with payload '{PayloadId}' rejected: the order to change is not found.")]
    private partial void LogOrderToChangeNotFound(string orderId, string payloadId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Punchout order request '{OrderId}' with payload '{PayloadId}' references unknown payload '{PreviousPayloadId}'.")]
    private partial void LogUnknownDocumentReference(string orderId, string payloadId, string previousPayloadId);
}
