using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using FluentValidation;
using GraphQL;
using MediatR;
using Microsoft.Extensions.Logging;
using VirtoCommerce.CartModule.Core.Model;
using VirtoCommerce.OrdersModule.Core.Model;
using VirtoCommerce.OrdersModule.Core.Services;
using VirtoCommerce.PaymentModule.Core.Model;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Punchout.Core.Cxml;
using VirtoCommerce.Punchout.Core.Cxml.Services;
using VirtoCommerce.Punchout.Core.Models;
using VirtoCommerce.Punchout.Core.Services;
using VirtoCommerce.ShippingModule.Core.Model;
using VirtoCommerce.Xapi.Core.Helpers;
using VirtoCommerce.XCart.Core;
using VirtoCommerce.XCart.Core.Queries;
using VirtoCommerce.XCart.Core.Services;
using VirtoCommerce.XOrder.Core;
using VirtoCommerce.XOrder.Core.Commands;
using AddressType = VirtoCommerce.CoreModule.Core.Common.AddressType;
using CoreAddress = VirtoCommerce.CoreModule.Core.Common.Address;
using Address = VirtoCommerce.CartModule.Core.Model.Address;
using LineItem = VirtoCommerce.CartModule.Core.Model.LineItem;
using OrderAddress = VirtoCommerce.OrdersModule.Core.Model.Address;
using OrderLineItem = VirtoCommerce.OrdersModule.Core.Model.LineItem;
using Payment = VirtoCommerce.CartModule.Core.Model.Payment;
using Shipment = VirtoCommerce.CartModule.Core.Model.Shipment;
using ShipmentItem = VirtoCommerce.OrdersModule.Core.Model.ShipmentItem;

namespace VirtoCommerce.Punchout.ExperienceApi.Services;

/// <summary>
/// Default handler: the setup is not customized, the order message is built from the current user cart.
/// </summary>
public class DefaultPunchoutHandler(
    IMediator mediator,
    ICxmlSerializer cxmlSerializer,
    IPunchoutOrderMessageBuilder orderMessageBuilder,
    ICartAggregateRepository cartAggregateRepository,
    ICartAvailMethodsService availableCartMethodsService,
    ICustomerOrderService customerOrderService,
    ICustomerOrderTotalsCalculator customerOrderTotalsCalculator,
    ILogger<DefaultPunchoutHandler> logger)
    : IPunchoutHandler
{
    private const string CartNotFound = "CartNotFound";
    private const string CartIsEmpty = "CartIsEmpty";

    protected const string CancelledOrderStatus = "Cancelled";

    protected const string PriceFormat = "0.00";

    protected const string CartValidationRuleSet = "*";

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

    public virtual Task HandleOrderRequestAsync(PunchoutOrderRequestHandlerContext context)
    {
        var orderType = context.Request.OrderType;

        // An update without a processed previous version is created from the requisition, see the processor validation
        if (orderType.EqualsIgnoreCase(CxmlConstants.OrderRequestType.Update) && context.PreviousRequest is not null)
        {
            return UpdateOrderAsync(context);
        }

        if (orderType.EqualsIgnoreCase(CxmlConstants.OrderRequestType.Delete))
        {
            return CancelOrderAsync(context);
        }

        return CreateOrderFromRequestAsync(context);
    }

    /// <summary>
    /// Creates the customer order from the requisition cart.
    /// </summary>
    protected virtual async Task CreateOrderFromRequestAsync(PunchoutOrderRequestHandlerContext context)
    {
        var lineItemIdsError = ValidateLineItemIds(context.Request);
        if (lineItemIdsError != null)
        {
            context.Fail(PunchoutOrderStatus.InvalidRequest, lineItemIdsError);
            return;
        }

        var cachedCartAggregate = await SearchCartAsync(context);
        if (cachedCartAggregate == null)
        {
            context.Fail(PunchoutOrderStatus.InvalidRequest, "The order items do not match any cart.");
            return;
        }

        // The cart aggregate is cached and shared, the changes of a rejected order must not stay in it
        var cartAggregate = cachedCartAggregate.CloneTyped();

        // The cart wins: the order is created only when the purchase order matches the cart
        var errors = ReconcileCart(context.Request, cartAggregate);
        if (errors.Count > 0)
        {
            context.Fail(PunchoutOrderStatus.InvalidRequest, string.Join(" ", errors));
            return;
        }

        try
        {
            await SelectOrderItemsAsync(context.Request, cartAggregate);

            await cartAggregate.ChangePurchaseOrderNumber(context.Request.OrderId);

            foreach (var requestItem in context.Request.Items)
            {
                await cartAggregate.ChangePurchaseOrderLineNumberAsync(requestItem.LineItemId, requestItem.LineNumber);
            }

            await AddAddressesAsync(context, cartAggregate);
            await AddShipmentAsync(context, cartAggregate);
            await AddPaymentAsync(context, cartAggregate);

            // The cart is validated before it is saved, so a rejected order does not change the buyer cart
            var validationError = await ValidateCartAsync(cartAggregate);
            if (validationError != null)
            {
                context.Fail(PunchoutOrderStatus.InvalidRequest, validationError);
                return;
            }

            await cartAggregateRepository.SaveAsync(cartAggregate);
            var order = await CreateOrderAsync(cartAggregate);

            context.Result.CustomerOrderId = order.Order.Id;
            context.Result.PurchaseOrderNumber = context.Request.OrderId;
        }
        catch (ValidationException ex)
        {
            // A cart operation did not pass the platform validation
            context.Fail(PunchoutOrderStatus.InvalidRequest, GetValidationErrorMessage(ex));
            return;
        }
        catch (ExecutionError ex) when (ex.Code == Constants.ValidationErrorCode)
        {
            // The order creation did not pass the cart validation 
            context.Fail(PunchoutOrderStatus.InvalidRequest, GetValidationErrorMessage(ex));
            return;
        }

        await ClearCartAsync(cartAggregate.Cart.Id);
    }

    /// <summary>
    /// Applies a new version of the purchase order to the customer order created by the previous version.
    /// The update replaces the order content: the lines are matched by lineNumber, the missing lines are removed.
    /// </summary>
    protected virtual async Task UpdateOrderAsync(PunchoutOrderRequestHandlerContext context)
    {
        var orderId = context.PreviousRequest?.CustomerOrderId;
        var order = string.IsNullOrEmpty(orderId)
            ? null
            : await customerOrderService.GetByIdAsync(orderId, CustomerOrderResponseGroup.Full.ToString());

        if (order is null)
        {
            context.Fail(PunchoutOrderStatus.InvalidRequest, $"Order '{context.Request.OrderId}' is not found.");
            return;
        }

        var error = ValidateOrderUpdate(context, order) ?? ReconcileOrder(context.Request, order);
        if (error != null)
        {
            context.Fail(PunchoutOrderStatus.InvalidRequest, error);
            return;
        }

        UpdateOrderItems(context.Request, order);
        UpdateOrderAddresses(context, order);
        UpdateOrderPayments(order);

        // The totals are recalculated on save, the row version rejects a concurrent change of the order
        await customerOrderService.SaveChangesAsync([order]);

        context.Result.CustomerOrderId = order.Id;
        context.Result.PurchaseOrderNumber = context.Request.OrderId;
    }

    /// <summary>
    /// Checks that the customer order may still be changed. Returns the error message, or null.
    /// </summary>
    protected virtual string ValidateOrderUpdate(PunchoutOrderRequestHandlerContext context, CustomerOrder order)
    {
        if (!order.StoreId.EqualsIgnoreCase(context.Store.Id))
        {
            return $"Order '{context.Request.OrderId}' is not found.";
        }

        if (order.IsCancelled)
        {
            return $"Order '{context.Request.OrderId}' is cancelled.";
        }

        var statuses = GetUpdatableOrderStatuses(context.Configuration);
        if (!statuses.Contains(order.Status, StringComparer.OrdinalIgnoreCase))
        {
            return $"Order '{context.Request.OrderId}' cannot be changed in status '{order.Status}'.";
        }

        if (order.InPayments?.Any(IsPaymentProcessed) == true)
        {
            return $"Order '{context.Request.OrderId}' cannot be changed, its payment is already processed.";
        }

        return null;
    }

    protected virtual IList<string> GetUpdatableOrderStatuses(PunchoutConfiguration configuration)
    {
        return configuration.UpdatableOrderStatuses.IsNullOrEmpty()
            ? PunchoutConfiguration.DefaultUpdatableOrderStatuses.ToList()
            : configuration.UpdatableOrderStatuses;
    }

    /// <summary>
    /// A payment that is authorized, captured or refunded no longer matches a changed total.
    /// </summary>
    protected virtual bool IsPaymentProcessed(PaymentIn payment)
    {
        if (payment.IsCancelled)
        {
            return false;
        }

        return payment.IsApproved ||
               payment.CapturedDate.HasValue ||
               payment.AuthorizedDate.HasValue ||
               payment.PaymentStatus is PaymentStatus.Authorized or PaymentStatus.Paid or PaymentStatus.PartiallyRefunded or PaymentStatus.Refunded;
    }

    /// <summary>
    /// Compares the purchase order with the customer order. Only the lines created from a purchase order line (with a line number)
    /// are changed by an update, lines cannot be added. Returns the mismatches, or null when the update can be applied.
    /// </summary>
    protected virtual string ReconcileOrder(PunchoutOrderRequestData request, CustomerOrder order)
    {
        if (!request.Currency.EqualsIgnoreCase(order.Currency))
        {
            return $"The order currency '{request.Currency}' does not match the current order currency '{order.Currency}'.";
        }

        var lineNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var errors = new List<string>();

        foreach (var item in request.Items)
        {
            if (string.IsNullOrEmpty(item.LineNumber))
            {
                errors.Add($"Item '{item.SupplierPartId}': lineNumber is missing.");
                continue;
            }

            if (!lineNumbers.Add(item.LineNumber))
            {
                errors.Add($"Line {item.LineNumber}: the line number is used by another line.");
                continue;
            }

            var lineItem = FindOrderLineItem(order, item.LineNumber);

            var error = lineItem is null
                ? $"Line {item.LineNumber}: the line is not in the order, an update cannot add lines."
                : ReconcileOrderItem(item, lineItem, order);

            if (error != null)
            {
                errors.Add(error);
            }
        }

        return errors.Count > 0 ? string.Join(" ", errors) : null;
    }

    /// <summary>
    /// Compares a purchase order line with the customer order line. Returns the mismatch, or null when they match.
    /// </summary>
    protected virtual string ReconcileOrderItem(PunchoutOrderRequestItem item, OrderLineItem lineItem, CustomerOrder order)
    {
        if (!item.SupplierPartId.EqualsIgnoreCase(lineItem.Sku))
        {
            return $"Line {item.LineNumber}: SupplierPartID '{item.SupplierPartId}' does not match the order item '{lineItem.Sku}'.";
        }

        if (item.Quantity != decimal.Truncate(item.Quantity) || item.Quantity > int.MaxValue)
        {
            return $"Line {item.LineNumber}: quantity {item.Quantity.ToString(CultureInfo.InvariantCulture)} must be a whole number.";
        }

        if (item.UnitPrice is null || RoundPrice(item.UnitPrice.Value) != RoundPrice(lineItem.PlacedPrice))
        {
            return $"Line {item.LineNumber}: unit price {item.UnitPrice?.ToString(PriceFormat, CultureInfo.InvariantCulture)} does not match the order price {lineItem.PlacedPrice.ToString(PriceFormat, CultureInfo.InvariantCulture)}.";
        }

        // A line split between shipments or packages cannot be changed automatically
        if ((int)item.Quantity != lineItem.Quantity && GetShipmentItems(order, lineItem.Id).Count > 1)
        {
            return $"Line {item.LineNumber}: the quantity cannot be changed, the line is split between shipments.";
        }

        return null;
    }

    protected virtual OrderLineItem FindOrderLineItem(CustomerOrder order, string lineNumber)
    {
        return order.Items?.FirstOrDefault(x => x.PurchaseOrderLineNumber.EqualsIgnoreCase(lineNumber));
    }

    /// <summary>
    /// Changes the quantities of the lines in the purchase order and removes the lines missing from it.
    /// Lines without a purchase order line number, e.g. gifts, are kept.
    /// </summary>
    protected virtual void UpdateOrderItems(PunchoutOrderRequestData request, CustomerOrder order)
    {
        var requestItems = request.Items.ToDictionary(x => x.LineNumber, StringComparer.OrdinalIgnoreCase);
        var lineItems = order.Items?.Where(x => !string.IsNullOrEmpty(x.PurchaseOrderLineNumber)).ToList() ?? [];

        foreach (var lineItem in lineItems)
        {
            if (requestItems.TryGetValue(lineItem.PurchaseOrderLineNumber, out var requestItem))
            {
                ChangeOrderItemQuantity(order, lineItem, (int)requestItem.Quantity);
            }
            else
            {
                RemoveOrderItem(order, lineItem);
            }
        }
    }

    protected virtual void ChangeOrderItemQuantity(CustomerOrder order, OrderLineItem lineItem, int quantity)
    {
        if (lineItem.Quantity == quantity)
        {
            return;
        }

        lineItem.Quantity = quantity;

        foreach (var shipmentItem in GetShipmentItems(order, lineItem.Id))
        {
            shipmentItem.Quantity = quantity;
        }
    }

    protected virtual void RemoveOrderItem(CustomerOrder order, OrderLineItem lineItem)
    {
        order.Items.Remove(lineItem);

        foreach (var shipment in order.Shipments ?? [])
        {
            RemoveShipmentItems(shipment.Items, lineItem.Id);

            foreach (var package in shipment.Packages ?? [])
            {
                RemoveShipmentItems(package.Items, lineItem.Id);
            }
        }
    }

    private static void RemoveShipmentItems(ICollection<ShipmentItem> shipmentItems, string lineItemId)
    {
        if (shipmentItems is null)
        {
            return;
        }

        // ICollection.Remove compares entities by id, the remaining items are kept by reference
        var remainingItems = shipmentItems.Where(x => x.LineItemId != lineItemId).ToList();

        shipmentItems.Clear();

        foreach (var shipmentItem in remainingItems)
        {
            shipmentItems.Add(shipmentItem);
        }
    }

    protected virtual IList<ShipmentItem> GetShipmentItems(CustomerOrder order, string lineItemId)
    {
        return (order.Shipments ?? [])
            .SelectMany(x => (x.Items ?? []).Concat((x.Packages ?? []).SelectMany(p => p.Items ?? [])))
            .Where(x => x.LineItemId == lineItemId)
            .ToList();
    }

    /// <summary>
    /// The purchase order addresses replace the order, shipment and payment addresses.
    /// </summary>
    protected virtual void UpdateOrderAddresses(PunchoutOrderRequestHandlerContext context, CustomerOrder order)
    {
        var shippingAddress = MapAddress<OrderAddress>(context.Request.ShipTo, AddressType.Shipping);
        var billingAddress = MapAddress<OrderAddress>(context.Request.BillTo, AddressType.Billing);

        order.Addresses ??= new List<OrderAddress>();

        if (shippingAddress != null)
        {
            ReplaceOrderAddress(order.Addresses, shippingAddress);

            foreach (var shipment in order.Shipments ?? [])
            {
                shipment.DeliveryAddress = MapAddress<OrderAddress>(context.Request.ShipTo, AddressType.Shipping);
            }
        }

        if (billingAddress != null)
        {
            ReplaceOrderAddress(order.Addresses, billingAddress);

            foreach (var payment in order.InPayments ?? [])
            {
                payment.BillingAddress = MapAddress<OrderAddress>(context.Request.BillTo, AddressType.Billing);
            }
        }
    }

    protected virtual void ReplaceOrderAddress(ICollection<OrderAddress> addresses, OrderAddress address)
    {
        foreach (var existingAddress in addresses.Where(x => x.AddressType == address.AddressType).ToList())
        {
            addresses.Remove(existingAddress);
        }

        addresses.Add(address);
    }

    /// <summary>
    /// The shipment price is kept. The payment covers the new order total, as for a new order.
    /// </summary>
    protected virtual void UpdateOrderPayments(CustomerOrder order)
    {
        var payment = order.InPayments?.FirstOrDefault(x => !x.IsCancelled);
        if (payment is null)
        {
            return;
        }

        customerOrderTotalsCalculator.CalculateTotals(order);
        payment.Sum = order.Total;
    }

    /// <summary>
    /// A 'delete' OrderRequest cancels the customer order created by the previous version, nothing is deleted.
    /// </summary>
    protected virtual async Task CancelOrderAsync(PunchoutOrderRequestHandlerContext context)
    {
        // No version of the purchase order created a customer order, the cancellation has nothing to change
        if (string.IsNullOrEmpty(context.PreviousRequest?.CustomerOrderId))
        {
            logger.LogInformation("Punchout order '{PurchaseOrderNumber}' has no customer order, the cancellation is accepted without changes.",
                context.Request.OrderId);

            context.Result.PurchaseOrderNumber = context.Request.OrderId;
            return;
        }

        var orderId = context.PreviousRequest.CustomerOrderId;
        var order = string.IsNullOrEmpty(orderId)
            ? null
            : await customerOrderService.GetByIdAsync(orderId, CustomerOrderResponseGroup.Full.ToString());

        if (order is null || !order.StoreId.EqualsIgnoreCase(context.Store.Id))
        {
            context.Fail(PunchoutOrderStatus.InvalidRequest, $"Order '{context.Request.OrderId}' is not found.");
            return;
        }

        context.Result.CustomerOrderId = order.Id;
        context.Result.PurchaseOrderNumber = context.Request.OrderId;

        // The buyer may send the delete again with a new payloadID, the order stays cancelled by the first one
        if (order.IsCancelled)
        {
            logger.LogInformation("Punchout order '{PurchaseOrderNumber}' is already cancelled, customer order '{CustomerOrderId}'.",
                context.Request.OrderId, order.Id);
            return;
        }

        var error = ValidateOrderCancellation(context, order);
        if (error != null)
        {
            context.Fail(PunchoutOrderStatus.InvalidRequest, error);
            return;
        }

        CancelOrder(context.Request, order);

        await customerOrderService.SaveChangesAsync([order]);
    }

    /// <summary>
    /// Checks that the customer order may still be cancelled. Returns the error message, or null.
    /// The processed payments do not block the cancellation, they get a cancellation request.
    /// </summary>
    protected virtual string ValidateOrderCancellation(PunchoutOrderRequestHandlerContext context, CustomerOrder order)
    {
        var statuses = GetUpdatableOrderStatuses(context.Configuration);

        return statuses.Contains(order.Status, StringComparer.OrdinalIgnoreCase)
            ? null
            : $"Order '{context.Request.OrderId}' cannot be cancelled in status '{order.Status}'.";
    }

    /// <summary>
    /// Requests the cancellation of the payments and cancels the order. The payload id of the delete request is the cancel reason.
    /// </summary>
    protected virtual void CancelOrder(PunchoutOrderRequestData request, CustomerOrder order)
    {
        // A payment that is already cancelled keeps its state
        foreach (var payment in order.InPayments?.Where(x => !x.IsCancelled && x.CancelledState != CancelledState.Completed) ?? [])
        {
            payment.CancelledState = CancelledState.Requested;
        }

        order.IsCancelled = true;
        order.Status = CancelledOrderStatus;
        order.CancelledDate = DateTime.UtcNow;
        order.CancelReason = request.PayloadId;
    }

    /// <summary>
    /// Temporary, until punchout gets its own cart: clear the current cart completely (unselected items, addresses)  
    /// </summary>
    protected virtual async Task ClearCartAsync(string cartId)
    {
        try
        {
            var cartAggregate = await cartAggregateRepository.GetCartByIdAsync(cartId);
            var cart = cartAggregate?.Cart;

            if (cart is null || (cart.Items.IsNullOrEmpty() && cart.Addresses.IsNullOrEmpty()))
            {
                return;
            }

            cart.Items?.Clear();
            cart.Addresses?.Clear();

            await cartAggregateRepository.SaveAsync(cartAggregate);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Punchout cart '{CartId}' cannot be cleared after the order is created.", cartId);
        }
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
    /// The order message sends the cart line item id in SupplierPartAuxiliaryID, the buyer returns it unchanged.
    /// </summary>
    protected virtual IList<string> GetLineItemIds(PunchoutOrderRequestData request)
    {
        return request.Items
            .Select(x => x.LineItemId)
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
            if (string.IsNullOrEmpty(item.LineItemId))
            {
                return $"Line {item.LineNumber}: SupplierPartAuxiliaryID is missing.";
            }

            if (!lineItemIds.Add(item.LineItemId))
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
            var lineItem = cart.Items?.FirstOrDefault(x => x.Id.EqualsIgnoreCase(item.LineItemId));

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
    /// Adds the default shipping method with the ShipTo address.
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

            if (rate is not null)
            {
                shipment.ShipmentMethodCode = rate.ShippingMethod.Code;
                shipment.ShipmentMethodOption = rate.OptionName;
                shipment.Price = rate.Rate;
            }
        }

        // Replace the shipment the buyer may have selected in the storefront
        shipment.Id = cartAggregate.Cart.Shipments?.FirstOrDefault()?.Id;

        shipment.Currency = cartAggregate.Cart.Currency;
        shipment.DeliveryAddress = MapAddress(context.Request.ShipTo, AddressType.Shipping);

        await cartAggregate.AddShipmentAsync(shipment, availableRates);
    }

    /// <summary>
    /// Adds the default payment method with the BillTo address.
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

            if (method is not null)
            {
                payment.PaymentGatewayCode = method.Code;
            }
        }

        // Replace the payment the buyer may have selected in the storefront
        payment.Id = cartAggregate.Cart.Payments?.FirstOrDefault()?.Id;
        payment.Currency = cartAggregate.Cart.Currency;

        // The total must include only the order items and the default shipment
        await cartAggregate.RecalculateAsync();
        payment.Amount = cartAggregate.Cart.Total;
        payment.BillingAddress = MapAddress(context.Request.BillTo, AddressType.Billing);

        await cartAggregate.AddPaymentAsync(payment, availableMethods);
    }

    /// <summary>
    /// Validates the cart with the same rules as the order creation
    /// </summary>
    protected virtual async Task<string> ValidateCartAsync(CartAggregate cartAggregate)
    {
        // The cart was changed after it was loaded, the cached validation results are stale
        cartAggregate.ClearValidationCache();

        var errors = (await cartAggregate.ValidateAsync(CartValidationRuleSet))
            .Concat(cartAggregate.OperationValidationErrors)
            .Select(x => x.ErrorMessage)
            .Where(x => !string.IsNullOrEmpty(x))
            .Distinct()
            .ToList();

        return errors.Count > 0
            ? string.Join(" ", errors)
            : null;
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

    protected virtual string GetValidationErrorMessage(ExecutionError error)
    {
        var messages = error.Data.Values
            .OfType<string>()
            .Where(x => !string.IsNullOrEmpty(x))
            .Distinct()
            .ToList();

        return messages.Count > 0
            ? string.Join(" ", messages)
            : error.Message;
    }

    protected virtual Address MapAddress(PunchoutAddress punchoutAddress, AddressType addressType)
    {
        return MapAddress<Address>(punchoutAddress, addressType);
    }

    protected virtual TAddress MapAddress<TAddress>(PunchoutAddress punchoutAddress, AddressType addressType)
        where TAddress : CoreAddress
    {
        if (punchoutAddress is null)
        {
            return null;
        }

        var address = AbstractTypeFactory<TAddress>.TryCreateInstance();

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
        var command = CreateCreateOrderFromCartCommand(cartAggregate);
        var order = await mediator.Send(command);
        return order;
    }

    protected virtual CreateOrderFromCartCommand CreateCreateOrderFromCartCommand(CartAggregate cartAggregate)
    {
        return new CreateOrderFromCartCommand(cartAggregate.Cart.Id);
    }
}
