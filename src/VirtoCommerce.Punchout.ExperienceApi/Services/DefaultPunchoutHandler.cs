using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using MediatR;
using VirtoCommerce.CartModule.Core.Model;
using VirtoCommerce.PaymentModule.Core.Model;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Punchout.Core.Cxml.Services;
using VirtoCommerce.Punchout.Core.Models;
using VirtoCommerce.Punchout.Core.Services;
using VirtoCommerce.ShippingModule.Core.Model;
using VirtoCommerce.XCart.Core;
using VirtoCommerce.XCart.Core.Queries;
using VirtoCommerce.XCart.Core.Services;
using VirtoCommerce.XOrder.Core;
using VirtoCommerce.XOrder.Core.Services;
using AddressType = VirtoCommerce.CoreModule.Core.Common.AddressType;

namespace VirtoCommerce.Punchout.ExperienceApi.Services;

/// <summary>
/// Default handler: the setup is not customized, the order message is built from the current user cart.
/// </summary>
public class DefaultPunchoutHandler(
    IMediator mediator,
    ICxmlSerializer cxmlSerializer,
    IPunchoutOrderMessageBuilder orderMessageBuilder,
    ICartAggregateRepository cartAggregateRepository,
    ICustomerOrderAggregateRepository customerOrderAggregateRepository,
    ICartAvailMethodsService availableCartMethodsService)
    : IPunchoutHandler
{
    private const string CartNotFound = "CartNotFound";
    private const string CartIsEmpty = "CartIsEmpty";

    protected const string PriceFormat = "0.00";

    public virtual Task HandleSetupAsync(PunchoutSetupHandlerContext context)
    {
        return Task.CompletedTask;
    }

    public virtual async Task HandleOrderMessageAsync(PunchoutOrderMessageHandlerContext context)
    {
        var cartAggregate = await GetCartAsync(context);

        if (cartAggregate is null)
        {
            context.Fail(CartNotFound);
            return;
        }

        if (cartAggregate.Cart.Items?.Any(x => x.SelectedForCheckout) != true)
        {
            context.Fail(CartIsEmpty);
            return;
        }

        var document = orderMessageBuilder.Build(context.Session, cartAggregate);

        context.Result.Cxml = cxmlSerializer.Serialize(document);
    }

    public virtual async Task HandleOrderRequestAsync(PunchoutOrderRequestHandlerContext context)
    {
        var lineItemIdsError = ValidateLineItemIds(context.Request);
        if (lineItemIdsError != null)
        {
            context.Fail(PunchoutOrderStatus.InvalidRequest, lineItemIdsError);
            return;
        }

        var cartAggregate = await SearchCartAsync(context);
        if (cartAggregate == null)
        {
            context.Fail(PunchoutOrderStatus.InvalidRequest, "The order items do not match any cart.");
            return;
        }

        // The cart wins: the order is created only when the purchase order matches the cart
        var errors = ReconcileCart(context.Request, cartAggregate);
        if (errors.Count > 0)
        {
            context.Fail(PunchoutOrderStatus.InvalidRequest, string.Join(" ", errors));
            return;
        }

        await SelectOrderItemsAsync(context.Request, cartAggregate);

        await cartAggregate.ChangePurchaseOrderNumber(context.Request.OrderId);
        await AddAddressesAsync(context, cartAggregate);
        await AddShipmentAsync(context, cartAggregate);
        await AddPaymentAsync(context, cartAggregate);

        await cartAggregateRepository.SaveAsync(cartAggregate);
        var order = await CreateOrderAsync(cartAggregate);

        context.Result.CustomerOrderId = order.Order.Id;
        context.Result.PurchaseOrderNumber = context.Request.OrderId;
    }

    protected virtual Task<CartAggregate> GetCartAsync(PunchoutOrderMessageHandlerContext context)
    {
        return mediator.Send(CreateGetCartQuery(context));
    }

    protected virtual GetCartQuery CreateGetCartQuery(PunchoutOrderMessageHandlerContext context)
    {
        var request = context.Request;
        var query = AbstractTypeFactory<GetCartQuery>.TryCreateInstance();

        query.StoreId = request.StoreId;
        query.UserId = request.UserId;
        query.OrganizationId = request.OrganizationId;
        query.CurrencyCode = request.CurrencyCode;
        query.CultureName = request.CultureName;

        return query;
    }

    protected virtual SearchCartQuery CreateSearchCartQuery(PunchoutOrderRequestHandlerContext context)
    {
        var lineItemIds = GetLineItemIds(context.Request);
        if (lineItemIds.Count == 0)
        {
            return null;
        }

        var query = AbstractTypeFactory<SearchCartQuery>.TryCreateInstance();
        query.StoreId = context.Store.Id;
        query.UserId = context.UserMapping.UserId;
        query.LineItemIds = lineItemIds;
        query.Take = 1;

        return query;
    }

    /// <summary>
    /// The order message sends the cart line item id as SupplierPartAuxiliaryID, the buyer returns it unchanged.
    /// </summary>
    protected virtual IList<string> GetLineItemIds(PunchoutOrderRequestData request)
    {
        return request.Items
            .Select(x => x.SupplierPartAuxiliaryId)
            .Where(x => !string.IsNullOrEmpty(x))
            .Distinct()
            .ToList();
    }

    /// <summary>
    /// Every order line must reference exactly one cart line item. Returns the error message, or null when the ids are valid.
    /// </summary>
    protected virtual string ValidateLineItemIds(PunchoutOrderRequestData request)
    {
        var lineItemIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in request.Items)
        {
            if (string.IsNullOrEmpty(item.SupplierPartAuxiliaryId))
            {
                return $"Line {item.LineNumber}: SupplierPartAuxiliaryID is missing.";
            }

            if (!lineItemIds.Add(item.SupplierPartAuxiliaryId))
            {
                return $"Line {item.LineNumber}: SupplierPartAuxiliaryID '{item.SupplierPartAuxiliaryId}' is used by another line.";
            }
        }

        return null;
    }

    /// <summary>
    /// Compares the purchase order with the cart. Returns the mismatches, empty when the order matches the cart.
    /// </summary>
    protected virtual IList<string> ReconcileCart(PunchoutOrderRequestData request, CartAggregate cartAggregate)
    {
        var errors = new List<string>();
        var cart = cartAggregate.Cart;

        if (!request.Currency.EqualsIgnoreCase(cart.Currency))
        {
            errors.Add($"The order currency '{request.Currency}' does not match the cart currency '{cart.Currency}'.");
        }

        foreach (var item in request.Items)
        {
            var lineItem = cart.Items?.FirstOrDefault(x => x.Id.EqualsIgnoreCase(item.SupplierPartAuxiliaryId));

            var error = lineItem is null
                ? $"Line {item.LineNumber}: item '{item.SupplierPartAuxiliaryId}' is not in the cart."
                : ReconcileItem(item, lineItem);

            if (error != null)
            {
                errors.Add(error);
            }
        }

        return errors;
    }

    /// <summary>
    /// Compares an order line with the cart line item. Returns the mismatch, or null when they match.
    /// </summary>
    protected virtual string ReconcileItem(PunchoutOrderRequestItem item, LineItem lineItem)
    {
        if (!item.SupplierPartId.EqualsIgnoreCase(lineItem.Sku))
        {
            return $"Line {item.LineNumber}: SupplierPartID '{item.SupplierPartId}' does not match the cart item '{lineItem.Sku}'.";
        }

        if (item.Quantity != lineItem.Quantity)
        {
            return $"Line {item.LineNumber}: quantity {item.Quantity.ToString(CultureInfo.InvariantCulture)} does not match the cart quantity {lineItem.Quantity}.";
        }

        if (item.UnitPrice is null || RoundPrice(item.UnitPrice.Value) != RoundPrice(lineItem.PlacedPrice))
        {
            return $"Line {item.LineNumber}: unit price {item.UnitPrice?.ToString(PriceFormat, CultureInfo.InvariantCulture)} does not match the cart price {lineItem.PlacedPrice.ToString(PriceFormat, CultureInfo.InvariantCulture)}.";
        }

        return null;
    }

    /// <summary>
    /// The order message sends the prices with two decimals, see <see cref="PunchoutOrderMessageBuilder"/>.
    /// </summary>
    protected virtual decimal RoundPrice(decimal price)
    {
        return Math.Round(price, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Selects for checkout only the cart items that are in the purchase order, so the order does not include items added after the punchout.
    /// </summary>
    protected virtual async Task SelectOrderItemsAsync(PunchoutOrderRequestData request, CartAggregate cartAggregate)
    {
        var orderLineItemIds = new HashSet<string>(GetLineItemIds(request), StringComparer.OrdinalIgnoreCase);
        var cartItems = cartAggregate.Cart.Items ?? [];

        var itemsToSelect = cartItems
            .Where(x => !x.SelectedForCheckout && orderLineItemIds.Contains(x.Id))
            .Select(x => x.Id)
            .ToList();

        var itemsToUnselect = cartItems
            .Where(x => x.SelectedForCheckout && !orderLineItemIds.Contains(x.Id))
            .Select(x => x.Id)
            .ToList();

        if (itemsToSelect.Count > 0)
        {
            await cartAggregate.ChangeItemsSelectedAsync(itemsToSelect, selectedForCheckout: true);
        }

        if (itemsToUnselect.Count > 0)
        {
            await cartAggregate.ChangeItemsSelectedAsync(itemsToUnselect, selectedForCheckout: false);
        }
    }

    protected virtual async Task AddAddressesAsync(PunchoutOrderRequestHandlerContext context, CartAggregate cartAggregate)
    {
        var request = context.Request;
        var shippingAddress = MapAddress(request.ShipTo, AddressType.Shipping);
        if (shippingAddress != null)
        {
            await cartAggregate.AddOrUpdateCartAddressByTypeAsync(shippingAddress);
        }

        var billingAddress = MapAddress(request.BillTo, AddressType.Billing);
        if (billingAddress != null)
        {
            await cartAggregate.AddOrUpdateCartAddressByTypeAsync(billingAddress);
        }
    }

    /// <summary>
    /// Adds the default shipping method with the ShipTo address. Returns the error message when the method is not available, otherwise null.
    /// </summary>
    protected virtual async Task AddShipmentAsync(PunchoutOrderRequestHandlerContext context, CartAggregate cartAggregate)
    {
        var shipment = AbstractTypeFactory<Shipment>.TryCreateInstance();

        var methodCode = context.Configuration.DefaultShipmentMethodCode;
        var availableRates = new List<ShippingRate>();

        if (!methodCode.IsNullOrEmpty())
        {
            availableRates = (await availableCartMethodsService.GetAvailableShippingRatesAsync(cartAggregate)).ToList();
            var rate = availableRates.FirstOrDefault(x => x.ShippingMethod?.Code.EqualsIgnoreCase(methodCode) == true);

            shipment.ShipmentMethodCode = rate.ShippingMethod.Code;
            shipment.ShipmentMethodOption = rate.OptionName;
        }

        // Replace the shipment the buyer may have selected in the storefront
        shipment.Id = cartAggregate.Cart.Shipments?.FirstOrDefault()?.Id;

        shipment.Currency = cartAggregate.Cart.Currency;
        shipment.DeliveryAddress = MapAddress(context.Request.ShipTo, AddressType.Shipping);
        shipment.Price = context.Request.ShippingAmount ?? 0.0m;

        await cartAggregate.AddShipmentAsync(shipment, availableRates);
    }

    /// <summary>
    /// Adds the default payment method with the BillTo address. Returns the error message when the method is not available, otherwise null.
    /// </summary>
    protected virtual async Task AddPaymentAsync(PunchoutOrderRequestHandlerContext context, CartAggregate cartAggregate)
    {
        var payment = AbstractTypeFactory<Payment>.TryCreateInstance();

        var methodCode = context.Configuration.DefaultPaymentMethodCode;
        var availableMethods = new List<PaymentMethod>();

        if (!methodCode.IsNullOrEmpty())
        {
            availableMethods = (await availableCartMethodsService.GetAvailablePaymentMethodsAsync(cartAggregate)).ToList();
            var method = availableMethods.FirstOrDefault(x => x.Code.EqualsIgnoreCase(methodCode));

            payment.PaymentGatewayCode = method.Code;
        }

        // Replace the payment the buyer may have selected in the storefront
        payment.Id = cartAggregate.Cart.Payments?.FirstOrDefault()?.Id;
        payment.Currency = cartAggregate.Cart.Currency;
        // The cart is recalculated after the shipment is added, so the total includes the shipping
        payment.Amount = cartAggregate.Cart.Total;
        payment.BillingAddress = MapAddress(context.Request.BillTo, AddressType.Billing);

        await cartAggregate.AddPaymentAsync(payment, availableMethods);
    }

    protected virtual Address MapAddress(PunchoutAddress punchoutAddress, AddressType addressType)
    {
        if (punchoutAddress is null)
        {
            return null;
        }

        var address = AbstractTypeFactory<Address>.TryCreateInstance();

        address.AddressType = addressType;
        address.OuterId = punchoutAddress.AddressId;
        address.Name = punchoutAddress.Name;
        address.Organization = punchoutAddress.Name;
        address.Line1 = punchoutAddress.Line1;
        address.Line2 = punchoutAddress.Line2;
        address.City = punchoutAddress.City;
        address.RegionId = punchoutAddress.RegionName;
        address.RegionName = punchoutAddress.RegionName;
        address.PostalCode = punchoutAddress.PostalCode;
        address.CountryCode = GetCountryCode(punchoutAddress.CountryCode);
        address.CountryName = punchoutAddress.CountryName;
        address.Email = punchoutAddress.Email;

        // DeliverTo is the recipient, e.g. 'John Smith'
        var deliverTo = punchoutAddress.DeliverTo?.Trim() ?? string.Empty;
        var separatorIndex = deliverTo.IndexOf(' ');
        address.FirstName = separatorIndex > 0 ? deliverTo[..separatorIndex] : deliverTo.EmptyToNull();
        address.LastName = separatorIndex > 0 ? deliverTo[(separatorIndex + 1)..].Trim() : null;

        return address;
    }

    /// <summary>
    /// cXML sends ISO 3166-1 alpha-2 codes ('US'), the platform stores alpha-3 codes ('USA').
    /// </summary>
    protected virtual string GetCountryCode(string isoCountryCode)
    {
        if (string.IsNullOrEmpty(isoCountryCode) || isoCountryCode.Length != 2)
        {
            return isoCountryCode;
        }

        try
        {
            return new RegionInfo(isoCountryCode).ThreeLetterISORegionName;
        }
        catch (ArgumentException)
        {
            return isoCountryCode;
        }
    }

    protected virtual async Task<CartAggregate> SearchCartAsync(PunchoutOrderRequestHandlerContext context)
    {
        var searchQuery = CreateSearchCartQuery(context);
        if (searchQuery == null)
        {
            return null;
        }

        var results = await mediator.Send(searchQuery);
        if (results.TotalCount != 1)
        {
            return null;
        }

        return results.Results.FirstOrDefault();
    }

    protected virtual async Task<CustomerOrderAggregate> CreateOrderAsync(CartAggregate cartAggregate)
    {
        var order = await customerOrderAggregateRepository.CreateOrderFromCart(cartAggregate.Cart);
        return order;
    }
}
