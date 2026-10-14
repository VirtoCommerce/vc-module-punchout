using System;
using System.Linq;
using System.Threading.Tasks;
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
    ILogger<PunchoutOrderRequestProcessor> logger)
    : BasePunchoutProcessor(options, userMappingSearchService), IPunchoutOrderRequestProcessor
{
    public virtual async Task<PunchoutOrderResult> ProcessAsync(PunchoutOrderRequestData request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validationResult = await ValidateAsync(request);

        if (!validationResult.IsValid)
        {
            return validationResult.Error;
        }

        // todo: create the order
        return PunchoutOrderResult.Error(PunchoutOrderStatus.OrderError, "Order processing is not implemented yet.");
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

            return PunchoutOrderValidationResult.Invalid(PunchoutOrderStatus.UserNotFound);
        }

        var store = await storeService.GetPunchoutStoreAsync(settings.StoreId);

        if (store is null)
        {
            logger.LogWarning("Punchout order request rejected for sender identity '{SenderIdentity}': store '{StoreId}' does not exist or punchout is disabled for it.",
                request.Sender, settings.StoreId);

            return PunchoutOrderValidationResult.Invalid(PunchoutOrderStatus.StoreNotConfigured);
        }

        if (!IsOrderTypeSupported(request.OrderType))
        {
            logger.LogWarning("Punchout order request '{OrderId}' rejected for sender identity '{SenderIdentity}': order type '{OrderType}' is not supported.",
                request.OrderId, request.Sender, request.OrderType);

            return PunchoutOrderValidationResult.Invalid(PunchoutOrderStatus.UnsupportedOrderType,
                $"Order type '{request.OrderType}' is not supported.");
        }

        var errorMessage = ValidateOrder(request);

        if (errorMessage is not null)
        {
            logger.LogWarning("Punchout order request '{OrderId}' rejected for sender identity '{SenderIdentity}': {Message}",
                request.OrderId, request.Sender, errorMessage);

            return PunchoutOrderValidationResult.Invalid(PunchoutOrderStatus.InvalidRequest, errorMessage);
        }

        if (!IsCurrencySupported(request.Currency, store))
        {
            logger.LogWarning("Punchout order request '{OrderId}' rejected for sender identity '{SenderIdentity}': currency '{Currency}' is not available in store '{StoreId}'.",
                request.OrderId, request.Sender, request.Currency, store.Id);

            return PunchoutOrderValidationResult.Invalid(PunchoutOrderStatus.CurrencyNotSupported,
                $"Currency '{request.Currency}' is not supported.");
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
        if (string.IsNullOrEmpty(request.OrderId))
        {
            return "The order has no orderID.";
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
}
