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

public partial class PunchoutCustomerOrderService(
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

    public virtual async Task CreateOrderAsync(PunchoutOrderRequestHandlerContext context)
    {
        if (string.IsNullOrEmpty(context.Customer?.UserId))
        {
            LogCustomerNotResolved(context.Request.OrderId);
            context.Fail(PunchoutOrderStatus.ConfigurationError, "The customer of the order is not resolved.");
            return;
        }

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
    /// An in-memory cart that the order builder converts to the customer order, as at checkout.
    /// </summary>
    protected virtual async Task<ShoppingCart> BuildCartAsync(PunchoutOrderRequestHandlerContext context)
    {
        var request = context.Request;
        var cart = AbstractTypeFactory<ShoppingCart>.TryCreateInstance();

        cart.StoreId = context.Store.Id;
        cart.Currency = request.Currency;
        cart.LanguageCode = context.Store.DefaultLanguage;
        cart.CustomerId = context.Customer.UserId;
        cart.CustomerName = context.Customer.UserName;
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

        // The payment amount needs the totals, including the shipment
        shoppingCartTotalsCalculator.CalculateTotals(cart);
        cart.Payments = [await CreatePaymentAsync(context, cart)];

        return cart;
    }

    /// <summary>
    /// The order of an organization mapping belongs to the mapped organization, the order of a user mapping to the first organization of the contact.
    /// </summary>
    protected virtual async Task SetCustomerAsync(PunchoutOrderRequestHandlerContext context, ShoppingCart cart)
    {
        var memberId = context.Customer.MemberId;
        var contact = string.IsNullOrEmpty(memberId)
            ? null
            : await memberService.GetByIdAsync(memberId) as Contact;

        if (contact is not null)
        {
            cart.CustomerName = contact.FullName.EmptyToNull() ?? contact.Name.EmptyToNull() ?? cart.CustomerName;
        }

        var organizationId = context.Customer.OrganizationId.EmptyToNull() ?? contact?.Organizations?.FirstOrDefault();
        if (string.IsNullOrEmpty(organizationId))
        {
            return;
        }

        var organization = await memberService.GetByIdAsync(organizationId);

        cart.OrganizationId = organizationId;
        cart.OrganizationName = organization?.Name;
    }

    /// <summary>
    /// Keeps the requisition line item id: the order builder maps the shipment items by it.
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

        // Calculators derive the placed price from list price and discount, so it stays the price sent to the buyer
        lineItem.ListPrice = Math.Max(requisitionItem.ListPrice, requisitionItem.PlacedPrice);
        lineItem.SalePrice = requisitionItem.SalePrice;
        lineItem.DiscountAmount = lineItem.ListPrice - requisitionItem.PlacedPrice;
        lineItem.PlacedPrice = requisitionItem.PlacedPrice;

        return lineItem;
    }

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

        // Rates depend on the delivery address
        cart.Shipments = [shipment];
        var evaluationContext = new ShippingEvaluationContext(cart);

        return methods
            .SelectMany(x => x.CalculateRates(evaluationContext))
            .FirstOrDefault(x => x.ShippingMethod?.Code.EqualsIgnoreCase(methodCode) == true);
    }

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

        // Totals are recalculated on save; the row version rejects a concurrent change
        await customerOrderService.SaveChangesAsync([order]);

        context.Result.CustomerOrderId = order.Id;
        context.Result.PurchaseOrderNumber = context.Request.OrderId;
    }

    protected virtual string ValidateOrderUpdate(PunchoutOrderRequestHandlerContext context, CustomerOrder order)
    {
        if (!order.StoreId.EqualsIgnoreCase(context.Store.Id))
        {
            return $"Order '{context.Request.OrderId}' is not found.";
        }

        var organizationError = ValidateOrderOrganization(context, order);
        if (organizationError != null)
        {
            return organizationError;
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

    /// <summary>
    /// An organization mapping changes only the orders of its organization. The orders of a user mapping are not checked.
    /// </summary>
    protected virtual string ValidateOrderOrganization(PunchoutOrderRequestHandlerContext context, CustomerOrder order)
    {
        var userMapping = context.UserMapping;

        if (userMapping?.IsOrganizationMapping != true || order.OrganizationId.EqualsIgnoreCase(userMapping.OrganizationId))
        {
            return null;
        }

        LogOrderOfAnotherOrganization(context.Request.OrderId, order.Id, order.OrganizationId, userMapping.OrganizationId);

        return $"Order '{context.Request.OrderId}' belongs to another organization.";
    }

    protected virtual IList<string> GetUpdatableOrderStatuses(PunchoutConfiguration configuration)
    {
        return configuration.UpdatableOrderStatuses.IsNullOrEmpty()
            ? PunchoutConfiguration.DefaultUpdatableOrderStatuses.ToList()
            : configuration.UpdatableOrderStatuses;
    }

    /// <summary>
    /// An authorized, captured or refunded payment cannot follow a changed total.
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
    /// Lines are matched by line number; a line missing from the order is added from its requisition item.
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
    /// SKU and price are already checked by the requisition service, only the quantity is left.
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

        // See CreateLineItem: keep the price sent to the buyer
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

        // Rebuilt instead of Remove: ICollection.Remove compares entities by id
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
    /// The shipment price is kept, the payment covers the new total.
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

    public virtual async Task CancelOrderAsync(PunchoutOrderRequestHandlerContext context)
    {
        // No customer order was created, nothing to cancel
        if (string.IsNullOrEmpty(context.PreviousRequest?.CustomerOrderId))
        {
            LogCancelWithoutCustomerOrder(context.Request.OrderId);

            context.Result.PurchaseOrderNumber = context.Request.OrderId;
            return;
        }

        var order = await customerOrderService.GetByIdAsync(context.PreviousRequest.CustomerOrderId, CustomerOrderResponseGroup.Full.ToString());

        if (order is null || !order.StoreId.EqualsIgnoreCase(context.Store.Id))
        {
            context.Fail(PunchoutOrderStatus.InvalidRequest, $"Order '{context.Request.OrderId}' is not found.");
            return;
        }

        var organizationError = ValidateOrderOrganization(context, order);
        if (organizationError != null)
        {
            context.Fail(PunchoutOrderStatus.InvalidRequest, organizationError);
            return;
        }

        context.Result.CustomerOrderId = order.Id;
        context.Result.PurchaseOrderNumber = context.Request.OrderId;

        // A delete resent with a new payloadID
        if (order.IsCancelled)
        {
            LogAlreadyCancelled(context.Request.OrderId, order.Id);
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
    /// Processed payments do not block the cancellation, they get a cancellation request.
    /// </summary>
    protected virtual string ValidateOrderCancellation(PunchoutOrderRequestHandlerContext context, CustomerOrder order)
    {
        var statuses = GetUpdatableOrderStatuses(context.Configuration);

        return statuses.Contains(order.Status, StringComparer.OrdinalIgnoreCase)
            ? null
            : $"Order '{context.Request.OrderId}' cannot be cancelled in status '{order.Status}'.";
    }

    protected virtual void CancelOrder(PunchoutOrderRequestData request, CustomerOrder order)
    {
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
    /// The order message sends prices with two decimals, see <see cref="PunchoutOrderMessageBuilder"/>.
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

        // DeliverTo is the recipient name, e.g. 'John Smith'
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

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Punchout order '{PurchaseOrderNumber}' has no customer order, the cancellation is accepted without changes.")]
    private partial void LogCancelWithoutCustomerOrder(string purchaseOrderNumber);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Punchout order '{PurchaseOrderNumber}' is already cancelled, customer order '{CustomerOrderId}'.")]
    private partial void LogAlreadyCancelled(string purchaseOrderNumber, string customerOrderId);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Punchout order '{PurchaseOrderNumber}' rejected: customer order '{CustomerOrderId}' belongs to organization '{OrderOrganizationId}', not to the mapped organization '{OrganizationId}'.")]
    private partial void LogOrderOfAnotherOrganization(string purchaseOrderNumber, string customerOrderId, string orderOrganizationId, string organizationId);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Punchout order '{PurchaseOrderNumber}' cannot be created: the handler context has no customer. An override of PunchoutOrderRequestProcessor.ValidateAsync or CreateHandlerContext must set PunchoutOrderValidationResult.Customer and PunchoutOrderRequestHandlerContext.Customer.")]
    private partial void LogCustomerNotResolved(string purchaseOrderNumber);
}
