using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using VirtoCommerce.CartModule.Core.Model;
using VirtoCommerce.CartModule.Core.Services;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.OrdersModule.Core.Model;
using VirtoCommerce.OrdersModule.Core.Services;
using VirtoCommerce.PaymentModule.Core.Model;
using VirtoCommerce.PaymentModule.Core.Model.Search;
using VirtoCommerce.PaymentModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Punchout.Core.Models;
using VirtoCommerce.ShippingModule.Core.Model;
using VirtoCommerce.ShippingModule.Core.Model.Search;
using VirtoCommerce.ShippingModule.Core.Services;
using Address = VirtoCommerce.CartModule.Core.Model.Address;
using AddressType = VirtoCommerce.CoreModule.Core.Common.AddressType;
using CoreAddress = VirtoCommerce.CoreModule.Core.Common.Address;
using LineItem = VirtoCommerce.CartModule.Core.Model.LineItem;
using OrderAddress = VirtoCommerce.OrdersModule.Core.Model.Address;
using OrderLineItem = VirtoCommerce.OrdersModule.Core.Model.LineItem;
using OrderShipmentItem = VirtoCommerce.OrdersModule.Core.Model.ShipmentItem;
using Payment = VirtoCommerce.CartModule.Core.Model.Payment;
using Shipment = VirtoCommerce.CartModule.Core.Model.Shipment;
using ShipmentItem = VirtoCommerce.CartModule.Core.Model.ShipmentItem;

namespace VirtoCommerce.Punchout.ExperienceApi.Services;

/// <summary>
/// Creates, updates and cancels the customer order of a purchase order. The order lines are created from the requisition items
/// matched by <see cref="Core.Services.IPunchoutRequisitionService"/>, with the prices sent to the buyer.
/// </summary>
public class PunchoutCustomerOrderService(
    ICustomerOrderService customerOrderService,
    ICustomerOrderTotalsCalculator customerOrderTotalsCalculator,
    ICustomerOrderBuilder customerOrderBuilder,
    IShoppingCartTotalsCalculator shoppingCartTotalsCalculator,
    IShippingMethodsSearchService shippingMethodsSearchService,
    IPaymentMethodsSearchService paymentMethodsSearchService,
    IMemberService memberService,
    ILogger<PunchoutCustomerOrderService> logger)
    : IPunchoutCustomerOrderService
{
    protected const string CancelledOrderStatus = "Cancelled";

    protected const string PriceFormat = "0.00";

    /// <summary>
    /// Creates the customer order from the requisition items the processor matched with the order lines.
    /// The prices are the prices sent to the buyer, they are not calculated again.
    /// </summary>
    public virtual async Task CreateOrderAsync(PunchoutOrderRequestHandlerContext context)
    {
        var missingItem = context.Request.Items.FirstOrDefault(x => x.RequisitionItem is null);
        if (missingItem != null)
        {
            context.Fail(PunchoutOrderStatus.InvalidRequest, $"Line {missingItem.LineNumber}: item '{missingItem.SupplierPartId}' is not in the punchout requisition.");
            return;
        }

        var cart = await BuildCartAsync(context);
        var order = await customerOrderBuilder.PlaceCustomerOrderFromCartAsync(cart);

        context.Result.CustomerOrderId = order.Id;
        context.Result.PurchaseOrderNumber = context.Request.OrderId;
    }

    /// <summary>
    /// Builds a cart that is not stored, the order builder converts it to the customer order as at the checkout.
    /// </summary>
    protected virtual async Task<ShoppingCart> BuildCartAsync(PunchoutOrderRequestHandlerContext context)
    {
        var request = context.Request;
        var cart = AbstractTypeFactory<ShoppingCart>.TryCreateInstance();

        cart.StoreId = context.Store.Id;
        cart.Currency = request.Currency;
        cart.LanguageCode = context.Store.DefaultLanguage;
        cart.CustomerId = context.UserMapping.UserId;
        cart.CustomerName = context.UserMapping.UserName;
        cart.PurchaseOrderNumber = request.OrderId;

        await SetCustomerAsync(context, cart);

        cart.Items = request.Items.Select(x => CreateLineItem(request, x)).ToList();
        cart.Addresses = new[]
            {
                MapAddress<Address>(request.ShipTo, AddressType.Shipping),
                MapAddress<Address>(request.BillTo, AddressType.Billing),
            }
            .Where(x => x != null)
            .ToList();
        cart.Shipments = [await CreateShipmentAsync(context, cart)];
        cart.Payments = [];

        // The payment covers the order total with the shipment
        shoppingCartTotalsCalculator.CalculateTotals(cart);
        cart.Payments = [await CreatePaymentAsync(context, cart)];

        return cart;
    }

    /// <summary>
    /// The customer name and the organization of the contact the user mapping belongs to.
    /// </summary>
    protected virtual async Task SetCustomerAsync(PunchoutOrderRequestHandlerContext context, ShoppingCart cart)
    {
        var memberId = context.UserMapping.MemberId;
        if (string.IsNullOrEmpty(memberId))
        {
            return;
        }

        if (await memberService.GetByIdAsync(memberId) is not Contact contact)
        {
            return;
        }

        cart.CustomerName = contact.FullName.EmptyToNull() ?? contact.Name.EmptyToNull() ?? cart.CustomerName;

        var organizationId = contact.Organizations?.FirstOrDefault();
        if (string.IsNullOrEmpty(organizationId))
        {
            return;
        }

        var organization = await memberService.GetByIdAsync(organizationId);

        cart.OrganizationId = organizationId;
        cart.OrganizationName = organization?.Name;
    }

    /// <summary>
    /// The cart line item from the requisition item. The line item id of the requisition is kept, the order builder maps the shipment items by it.
    /// </summary>
    protected virtual LineItem CreateLineItem(PunchoutOrderRequestData request, PunchoutOrderRequestItem item)
    {
        var requisitionItem = item.RequisitionItem;
        var lineItem = AbstractTypeFactory<LineItem>.TryCreateInstance();

        lineItem.Id = requisitionItem.LineItemId;
        lineItem.ProductId = requisitionItem.ProductId;
        lineItem.CatalogId = requisitionItem.CatalogId;
        lineItem.CategoryId = requisitionItem.CategoryId;
        lineItem.Sku = requisitionItem.Sku;
        lineItem.Name = requisitionItem.Name;
        lineItem.ImageUrl = requisitionItem.ImageUrl;
        lineItem.ProductType = requisitionItem.ProductType;
        lineItem.ProductOuterId = requisitionItem.ProductOuterId;
        lineItem.PriceId = requisitionItem.PriceId;
        lineItem.IsGift = requisitionItem.IsGift;
        lineItem.FulfillmentCenterId = requisitionItem.FulfillmentCenterId;
        lineItem.FulfillmentCenterName = requisitionItem.FulfillmentCenterName;
        lineItem.WeightUnit = requisitionItem.WeightUnit;
        lineItem.Weight = requisitionItem.Weight;
        lineItem.MeasureUnit = requisitionItem.MeasureUnit;
        lineItem.Height = requisitionItem.Height;
        lineItem.Length = requisitionItem.Length;
        lineItem.Width = requisitionItem.Width;
        lineItem.Quantity = (int)item.Quantity;
        lineItem.Currency = request.Currency;
        lineItem.TaxType = requisitionItem.TaxType;
        lineItem.TaxPercentRate = requisitionItem.TaxPercentRate;
        lineItem.PurchaseOrderLineNumber = item.LineNumber;
        lineItem.SelectedForCheckout = true;

        // The calculators set the placed price to the list price without the discount, it must stay the price sent to the buyer
        lineItem.ListPrice = Math.Max(requisitionItem.ListPrice, requisitionItem.PlacedPrice);
        lineItem.SalePrice = requisitionItem.SalePrice;
        lineItem.DiscountAmount = lineItem.ListPrice - requisitionItem.PlacedPrice;
        lineItem.PlacedPrice = requisitionItem.PlacedPrice;

        return lineItem;
    }

    /// <summary>
    /// One shipment with all the items, the configured default shipping method and the ShipTo address.
    /// </summary>
    protected virtual async Task<Shipment> CreateShipmentAsync(PunchoutOrderRequestHandlerContext context, ShoppingCart cart)
    {
        var shipment = AbstractTypeFactory<Shipment>.TryCreateInstance();

        shipment.Currency = cart.Currency;
        shipment.DeliveryAddress = MapAddress<Address>(context.Request.ShipTo, AddressType.Shipping);
        shipment.Items = cart.Items
            .Select(x =>
            {
                var shipmentItem = AbstractTypeFactory<ShipmentItem>.TryCreateInstance();
                shipmentItem.LineItemId = x.Id;
                shipmentItem.LineItem = x;
                shipmentItem.Quantity = x.Quantity;
                return shipmentItem;
            })
            .ToList();

        var rate = await GetShippingRateAsync(context, cart, shipment);
        if (rate is not null)
        {
            shipment.ShipmentMethodCode = rate.ShippingMethod.Code;
            shipment.ShipmentMethodOption = rate.OptionName;
            shipment.Price = rate.Rate;
        }

        return shipment;
    }

    /// <summary>
    /// The rate of the configured default shipping method, or null when it is not configured or not available.
    /// </summary>
    protected virtual async Task<ShippingRate> GetShippingRateAsync(PunchoutOrderRequestHandlerContext context, ShoppingCart cart, Shipment shipment)
    {
        var methodCode = context.Configuration.DefaultShipmentMethodCode;
        if (string.IsNullOrEmpty(methodCode))
        {
            return null;
        }

        var criteria = AbstractTypeFactory<ShippingMethodsSearchCriteria>.TryCreateInstance();
        criteria.StoreId = cart.StoreId;
        criteria.IsActive = true;
        criteria.Take = int.MaxValue;

        var methods = (await shippingMethodsSearchService.SearchAsync(criteria)).Results
            .Where(x => x.Code.EqualsIgnoreCase(methodCode))
            .ToList();

        // The rates are calculated for the cart with the delivery address
        cart.Shipments = [shipment];
        var evaluationContext = new ShippingEvaluationContext(cart);

        return methods
            .SelectMany(x => x.CalculateRates(evaluationContext))
            .FirstOrDefault(x => x.ShippingMethod?.Code.EqualsIgnoreCase(methodCode) == true);
    }

    /// <summary>
    /// The payment of the order total with the configured default payment method and the BillTo address.
    /// </summary>
    protected virtual async Task<Payment> CreatePaymentAsync(PunchoutOrderRequestHandlerContext context, ShoppingCart cart)
    {
        var payment = AbstractTypeFactory<Payment>.TryCreateInstance();

        payment.Currency = cart.Currency;
        payment.Amount = cart.Total;
        payment.BillingAddress = MapAddress<Address>(context.Request.BillTo, AddressType.Billing);

        var methodCode = context.Configuration.DefaultPaymentMethodCode;
        if (!string.IsNullOrEmpty(methodCode))
        {
            var criteria = AbstractTypeFactory<PaymentMethodsSearchCriteria>.TryCreateInstance();
            criteria.StoreId = cart.StoreId;
            criteria.IsActive = true;
            criteria.Codes = [methodCode];
            criteria.Take = 1;

            var method = (await paymentMethodsSearchService.SearchAsync(criteria)).Results.FirstOrDefault();
            payment.PaymentGatewayCode = method?.Code;
        }

        return payment;
    }

    /// <summary>
    /// Applies a new version of the purchase order to the customer order created by the previous version.
    /// The update replaces the order content: the lines are matched by lineNumber, the missing lines are removed,
    /// the new lines are added from the requisition items.
    /// </summary>
    public virtual async Task UpdateOrderAsync(PunchoutOrderRequestHandlerContext context)
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
    /// are changed by an update. A line that is not in the order is added from its requisition item.
    /// Returns the mismatches, or null when the update can be applied.
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
                ? ReconcileAddedItem(item)
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

    /// <summary>
    /// A line added by an update orders a requisition item. The processor compared its SKU and price, the quantity must match the requisition.
    /// Returns the mismatch, or null when the line can be added.
    /// </summary>
    protected virtual string ReconcileAddedItem(PunchoutOrderRequestItem item)
    {
        var requisitionItem = item.RequisitionItem;

        if (requisitionItem is null)
        {
            return $"Line {item.LineNumber}: the line is not in the order and not in a punchout requisition.";
        }

        if (item.Quantity != requisitionItem.Quantity)
        {
            return $"Line {item.LineNumber}: quantity {item.Quantity.ToString(CultureInfo.InvariantCulture)} does not match the requisition quantity {requisitionItem.Quantity}.";
        }

        return null;
    }

    protected virtual OrderLineItem FindOrderLineItem(CustomerOrder order, string lineNumber)
    {
        return order.Items?.FirstOrDefault(x => x.PurchaseOrderLineNumber.EqualsIgnoreCase(lineNumber));
    }

    /// <summary>
    /// Changes the quantities of the lines in the purchase order, removes the lines missing from it and adds the new lines.
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

        foreach (var requestItem in request.Items.Where(x => FindOrderLineItem(order, x.LineNumber) is null))
        {
            AddOrderItem(request, order, requestItem);
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

    /// <summary>
    /// Adds the requisition item to the order and to its first shipment.
    /// </summary>
    protected virtual void AddOrderItem(PunchoutOrderRequestData request, CustomerOrder order, PunchoutOrderRequestItem item)
    {
        var lineItem = CreateOrderLineItem(request, item);

        lineItem.Id ??= Guid.NewGuid().ToString();

        order.Items ??= new List<OrderLineItem>();
        order.Items.Add(lineItem);

        var shipment = order.Shipments?.FirstOrDefault(x => !x.IsCancelled);
        if (shipment is null)
        {
            return;
        }

        var shipmentItem = AbstractTypeFactory<OrderShipmentItem>.TryCreateInstance();
        shipmentItem.LineItem = lineItem;
        shipmentItem.LineItemId = lineItem.Id;
        shipmentItem.Quantity = lineItem.Quantity;

        shipment.Items ??= new List<OrderShipmentItem>();
        shipment.Items.Add(shipmentItem);
    }

    protected virtual OrderLineItem CreateOrderLineItem(PunchoutOrderRequestData request, PunchoutOrderRequestItem item)
    {
        var requisitionItem = item.RequisitionItem;
        var lineItem = AbstractTypeFactory<OrderLineItem>.TryCreateInstance();

        lineItem.ProductId = requisitionItem.ProductId;
        lineItem.CatalogId = requisitionItem.CatalogId;
        lineItem.CategoryId = requisitionItem.CategoryId;
        lineItem.Sku = requisitionItem.Sku;
        lineItem.Name = requisitionItem.Name;
        lineItem.ImageUrl = requisitionItem.ImageUrl;
        lineItem.ProductType = requisitionItem.ProductType;
        lineItem.ProductOuterId = requisitionItem.ProductOuterId;
        lineItem.PriceId = requisitionItem.PriceId;
        lineItem.IsGift = requisitionItem.IsGift;
        lineItem.FulfillmentCenterId = requisitionItem.FulfillmentCenterId;
        lineItem.FulfillmentCenterName = requisitionItem.FulfillmentCenterName;
        lineItem.WeightUnit = requisitionItem.WeightUnit;
        lineItem.Weight = requisitionItem.Weight;
        lineItem.MeasureUnit = requisitionItem.MeasureUnit;
        lineItem.Height = requisitionItem.Height;
        lineItem.Length = requisitionItem.Length;
        lineItem.Width = requisitionItem.Width;
        lineItem.Quantity = (int)item.Quantity;
        lineItem.Currency = request.Currency;
        lineItem.TaxType = requisitionItem.TaxType;
        lineItem.TaxPercentRate = requisitionItem.TaxPercentRate;
        lineItem.PurchaseOrderLineNumber = item.LineNumber;

        // The order calculator sets the placed price to the price without the discount
        lineItem.Price = Math.Max(requisitionItem.ListPrice, requisitionItem.PlacedPrice);
        lineItem.DiscountAmount = lineItem.Price - requisitionItem.PlacedPrice;
        lineItem.PlacedPrice = requisitionItem.PlacedPrice;

        return lineItem;
    }

    private static void RemoveShipmentItems(ICollection<OrderShipmentItem> shipmentItems, string lineItemId)
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

    protected virtual IList<OrderShipmentItem> GetShipmentItems(CustomerOrder order, string lineItemId)
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
    public virtual async Task CancelOrderAsync(PunchoutOrderRequestHandlerContext context)
    {
        // No version of the purchase order created a customer order, the cancellation has nothing to change
        if (string.IsNullOrEmpty(context.PreviousRequest?.CustomerOrderId))
        {
            logger.LogInformation("Punchout order '{PurchaseOrderNumber}' has no customer order, the cancellation is accepted without changes.",
                context.Request.OrderId);

            context.Result.PurchaseOrderNumber = context.Request.OrderId;
            return;
        }

        var order = await customerOrderService.GetByIdAsync(context.PreviousRequest.CustomerOrderId, CustomerOrderResponseGroup.Full.ToString());

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
    /// The order message sends the prices with two decimals, see <see cref="PunchoutOrderMessageBuilder"/>.
    /// </summary>
    protected virtual decimal RoundPrice(decimal price)
    {
        return Math.Round(price, 2, MidpointRounding.AwayFromZero);
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
}
