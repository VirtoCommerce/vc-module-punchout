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
/// Builds the order message from the user cart. Orders are processed by <see cref="IPunchoutRequisitionService"/>
/// and <see cref="IPunchoutCustomerOrderService"/>.
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
    /// Removes the sent items from the cart, so the next session does not send them again.
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

        // An update without a processed previous version is created as a new order
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

    protected virtual async Task CancelOrderAsync(PunchoutOrderRequestHandlerContext context)
    {
        await punchoutOrderService.CancelOrderAsync(context);

        if (!context.IsFailed)
        {
            await ReleaseItemsAsync(context, () => requisitionService.ReleasePurchaseOrderItemsAsync(context));
        }
    }

    /// <summary>
    /// Reserves the items before the order is changed and releases them if the change fails.
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
    /// A failure is logged and does not change the result: the customer order is already processed.
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
