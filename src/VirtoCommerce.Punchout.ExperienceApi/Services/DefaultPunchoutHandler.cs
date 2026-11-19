using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using VirtoCommerce.CartModule.Core.Model;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Punchout.Core.Cxml;
using VirtoCommerce.Punchout.Core.Cxml.Services;
using VirtoCommerce.Punchout.Core.Models;
using VirtoCommerce.Punchout.Core.Services;
using VirtoCommerce.XCart.Core;
using VirtoCommerce.XCart.Core.Queries;
using VirtoCommerce.XCart.Core.Services;

namespace VirtoCommerce.Punchout.ExperienceApi.Services;

/// <summary>
/// Default handler: the setup is not customized, the order message is built from the current user cart,
/// the customer order is created from the snapshot of the requisition items stored with the order messages.
/// The requisition items are matched and reserved by <see cref="IPunchoutRequisitionService"/>,
/// the customer order is changed by <see cref="IPunchoutCustomerOrderService"/>.
/// </summary>
public partial class DefaultPunchoutHandler(
    IMediator mediator,
    ICxmlSerializer cxmlSerializer,
    IPunchoutOrderMessageBuilder orderMessageBuilder,
    ICartAggregateRepository cartAggregateRepository,
    IPunchoutRequisitionService requisitionService,
    IPunchoutCustomerOrderService punchoutOrderService,
    ILogger<DefaultPunchoutHandler> logger)
    : IPunchoutHandler, IPunchoutOrderMessageReturnedHandler
{
    private const string CartNotFound = "CartNotFound";
    private const string CartIsEmpty = "CartIsEmpty";

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
        context.CartId = cartAggregate.Cart.Id;
        context.Items = BuildRequisitionItems(cartAggregate);
    }

    /// <summary>
    /// Removes the sent items from the cart: the buyer keeps them in its requisition,
    /// a next punchout session must not send them again.
    /// </summary>
    public virtual async Task HandleOrderMessageReturnedAsync(PunchoutOrderMessageHandlerContext context)
    {
        if (context.CartId.IsNullOrEmpty() || context.Items.IsNullOrEmpty())
        {
            return;
        }

        var cartAggregate = await cartAggregateRepository.GetCartByIdAsync(context.CartId);
        if (cartAggregate == null)
        {
            return;
        }

        var cartItems = cartAggregate.Cart.Items ?? [];

        var sentItemIds = context.Items
            .Select(x => x.LineItemId)
            .Where(id => cartItems.Any(x => x.Id == id))
            .ToArray();

        if (sentItemIds.Length == 0)
        {
            return;
        }

        await cartAggregate.RemoveItemsAsync(sentItemIds);
        await cartAggregateRepository.SaveAsync(cartAggregate);
    }

    /// <summary>
    /// The snapshot of the sent cart line items, the customer order is created from it.
    /// </summary>
    protected virtual IList<PunchoutRequisitionItem> BuildRequisitionItems(CartAggregate cartAggregate)
    {
        var cart = cartAggregate.Cart;

        return cart.Items
            .Where(x => x.SelectedForCheckout)
            .Select(x => BuildRequisitionItem(cart, x))
            .ToList();
    }

    protected virtual PunchoutRequisitionItem BuildRequisitionItem(ShoppingCart cart, LineItem lineItem)
    {
        var item = AbstractTypeFactory<PunchoutRequisitionItem>.TryCreateInstance();

        item.LineItemId = lineItem.Id;
        item.ProductId = lineItem.ProductId;
        item.CatalogId = lineItem.CatalogId;
        item.CategoryId = lineItem.CategoryId;
        item.Sku = lineItem.Sku;
        item.Name = lineItem.Name;
        item.ImageUrl = lineItem.ImageUrl;
        item.ProductType = lineItem.ProductType;
        item.ProductOuterId = lineItem.ProductOuterId;
        item.PriceId = lineItem.PriceId;
        item.IsGift = lineItem.IsGift;
        item.FulfillmentCenterId = lineItem.FulfillmentCenterId;
        item.FulfillmentCenterName = lineItem.FulfillmentCenterName;
        item.WeightUnit = lineItem.WeightUnit;
        item.Weight = lineItem.Weight;
        item.MeasureUnit = lineItem.MeasureUnit;
        item.Height = lineItem.Height;
        item.Length = lineItem.Length;
        item.Width = lineItem.Width;
        item.Quantity = lineItem.Quantity;
        item.Currency = lineItem.Currency ?? cart.Currency;
        item.ListPrice = lineItem.ListPrice;
        item.SalePrice = lineItem.SalePrice;
        item.DiscountAmount = lineItem.DiscountAmount;
        item.PlacedPrice = lineItem.PlacedPrice;
        item.TaxType = lineItem.TaxType;
        item.TaxPercentRate = lineItem.TaxPercentRate;

        return item;
    }

    public virtual Task HandleOrderRequestAsync(PunchoutOrderRequestHandlerContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

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

        return CreateOrderAsync(context);
    }

    /// <summary>
    /// Every order line must be a requisition item returned to the user and not ordered by another purchase order.
    /// </summary>
    protected virtual async Task CreateOrderAsync(PunchoutOrderRequestHandlerContext context)
    {
        var error = await requisitionService.MatchItemsAsync(context, isNewOrder: true);
        if (error != null)
        {
            context.Fail(PunchoutOrderStatus.InvalidRequest, error);
            return;
        }

        await ProcessReservedItemsAsync(context, () => punchoutOrderService.CreateOrderAsync(context));
    }

    /// <summary>
    /// The lines added by the update are requisition items, as in a new order. The items of the removed lines are released.
    /// </summary>
    protected virtual async Task UpdateOrderAsync(PunchoutOrderRequestHandlerContext context)
    {
        var error = await requisitionService.MatchItemsAsync(context, isNewOrder: false);
        if (error != null)
        {
            context.Fail(PunchoutOrderStatus.InvalidRequest, error);
            return;
        }

        await ProcessReservedItemsAsync(context, () => punchoutOrderService.UpdateOrderAsync(context));

        if (!context.IsFailed)
        {
            await ReleaseItemsAsync(context, () => requisitionService.ReleaseRemovedItemsAsync(context));
        }
    }

    /// <summary>
    /// The items of a cancelled purchase order are released and may be ordered by another purchase order.
    /// </summary>
    protected virtual async Task CancelOrderAsync(PunchoutOrderRequestHandlerContext context)
    {
        await punchoutOrderService.CancelOrderAsync(context);

        if (!context.IsFailed)
        {
            await ReleaseItemsAsync(context, () => requisitionService.ReleasePurchaseOrderItemsAsync(context));
        }
    }

    /// <summary>
    /// Reserves the requisition items before the customer order is changed, so a concurrent purchase order cannot order them.
    /// The reserved items are released when the customer order is not changed.
    /// </summary>
    protected virtual async Task ProcessReservedItemsAsync(PunchoutOrderRequestHandlerContext context, Func<Task> action)
    {
        var reservedLines = await requisitionService.ReserveItemsAsync(context);

        if (reservedLines is null)
        {
            context.Fail(PunchoutOrderStatus.InvalidRequest, "A requisition item of the order is already ordered by another purchase order.");
            return;
        }

        try
        {
            await action();
        }
        catch
        {
            await ReleaseItemsAsync(context, () => requisitionService.ReleaseItemsAsync(reservedLines));
            throw;
        }

        if (context.IsFailed)
        {
            await ReleaseItemsAsync(context, () => requisitionService.ReleaseItemsAsync(reservedLines));
        }
    }

    /// <summary>
    /// The customer order is already processed, a failure to release the items is logged and does not change the result.
    /// </summary>
    protected virtual async Task ReleaseItemsAsync(PunchoutOrderRequestHandlerContext context, Func<Task> release)
    {
        try
        {
            await release();
        }
        catch (Exception ex)
        {
            LogReleaseFailed(ex, context.Request.OrderId, context.OrderRequestId);
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

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Punchout order items of purchase order '{PurchaseOrderNumber}' cannot be released after request '{OrderRequestId}'.")]
    private partial void LogReleaseFailed(Exception ex, string purchaseOrderNumber, string orderRequestId);
}
