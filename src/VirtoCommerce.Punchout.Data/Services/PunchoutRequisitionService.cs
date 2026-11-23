using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Punchout.Core;
using VirtoCommerce.Punchout.Core.Models;
using VirtoCommerce.Punchout.Core.Services;

namespace VirtoCommerce.Punchout.Data.Services;

public partial class PunchoutRequisitionService(
    IPunchoutRequisitionItemService requisitionItemService,
    IPunchoutOrderLineService orderLineService,
    IPunchoutOrderLineSearchService orderLineSearchService,
    ILogger<PunchoutRequisitionService> logger)
    : IPunchoutRequisitionService
{
    protected const string PriceFormat = "0.00";

    public virtual async Task<string> MatchItemsAsync(PunchoutOrderRequestHandlerContext context, bool isNewOrder)
    {
        ArgumentNullException.ThrowIfNull(context);

        var request = context.Request;
        var errors = ValidateItemReferences(request.Items);

        if (errors.Count > 0)
        {
            return string.Join(" ", errors);
        }

        var configurationId = context.Configuration.Id;
        var lineItemIds = request.Items.Select(x => x.LineItemId).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var orderedLines = await GetOrderedLinesAsync(configurationId, lineItemIds);
        var requisitionItems = await requisitionItemService.GetReturnedItemsAsync(configurationId, context.UserMapping.UserId, lineItemIds);
        var purchaseOrderLines = isNewOrder ? [] : await GetPurchaseOrderLinesAsync(configurationId, request.OrderId);

        foreach (var item in request.Items)
        {
            item.RequisitionItem = null;

            var error = MatchItem(request, item, orderedLines, requisitionItems, purchaseOrderLines, isNewOrder);

            if (error is not null)
            {
                errors.Add(error);
            }
        }

        return errors.Count > 0 ? string.Join(" ", errors) : null;
    }

    /// <summary>
    /// Every line must reference a cart line item, and no two lines may reference the same one.
    /// </summary>
    protected virtual List<string> ValidateItemReferences(IEnumerable<PunchoutOrderRequestItem> items)
    {
        var errors = new List<string>();
        var references = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in items)
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

        return errors;
    }

    /// <summary>
    /// An existing line of the purchase order is compared with the ordered line, any other line is matched with a requisition item.
    /// </summary>
    protected virtual string MatchItem(PunchoutOrderRequestData request, PunchoutOrderRequestItem item, IList<PunchoutOrderLine> orderedLines,
        IList<PunchoutRequisitionItem> requisitionItems, IList<PunchoutOrderLine> purchaseOrderLines, bool isNewOrder)
    {
        var orderedLine = orderedLines.FirstOrDefault(x => IsSameLine(x, item));

        if (orderedLine is not null && !orderedLine.PurchaseOrderNumber.EqualsIgnoreCase(request.OrderId))
        {
            return $"Line {item.LineNumber}: item '{item.SupplierPartId}' is already ordered by purchase order '{orderedLine.PurchaseOrderNumber}'.";
        }

        if (!isNewOrder)
        {
            // Matched by line number, as the customer order lines
            var purchaseOrderLine = purchaseOrderLines.FirstOrDefault(x => x.LineNumber.EqualsIgnoreCase(GetStoredLineNumber(item)));

            if (purchaseOrderLine is not null)
            {
                return IsSameLine(purchaseOrderLine, item)
                    ? null
                    : $"Line {item.LineNumber}: the line orders another requisition item, an update cannot replace the item of a line.";
            }

            if (orderedLine is not null)
            {
                return $"Line {item.LineNumber}: item '{item.SupplierPartId}' is already ordered by line {orderedLine.LineNumber} of the purchase order.";
            }
        }

        return MatchRequisitionItem(request, item, requisitionItems, isNewOrder);
    }

    protected virtual string MatchRequisitionItem(PunchoutOrderRequestData request, PunchoutOrderRequestItem item,
        IList<PunchoutRequisitionItem> requisitionItems, bool isNewOrder)
    {
        var candidates = requisitionItems.Where(x => IsSameItem(x, item)).ToList();

        if (candidates.Count > 1)
        {
            return $"Line {item.LineNumber}: item '{item.SupplierPartId}' is in several punchout requisitions, SupplierPartAuxiliaryID '{item.SupplierPartAuxiliaryId}' has no session id.";
        }

        var requisitionItem = candidates.FirstOrDefault();

        if (requisitionItem is null)
        {
            return $"Line {item.LineNumber}: item '{item.SupplierPartId}' is not in a punchout requisition returned to the user.";
        }

        var error = ReconcileRequisitionItem(request, item, requisitionItem, isNewOrder);

        if (error is null)
        {
            item.RequisitionItem = requisitionItem;
        }

        return error;
    }

    public virtual async Task<IList<PunchoutOrderLine>> ReserveItemsAsync(PunchoutOrderRequestHandlerContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var request = context.Request;
        var configurationId = context.Configuration.Id;
        var existingLines = await GetPurchaseOrderLinesAsync(configurationId, request.OrderId);

        var lines = request.Items
            .Where(x => x.RequisitionItem is not null)
            .Where(x => !existingLines.Any(line => IsSameRequisitionItem(line, x.RequisitionItem)))
            .Select(x => CreateOrderLine(context, x))
            .ToList();

        if (lines.Count == 0)
        {
            return lines;
        }

        try
        {
            await orderLineService.SaveChangesAsync(lines);

            return lines;
        }
        catch (Exception ex)
        {
            // The unique index rejects an item ordered by a concurrent purchase order
            var orderedLines = await GetOrderedLinesAsync(configurationId, lines.Select(x => x.LineItemId).ToList());

            if (!orderedLines.Any(orderedLine => lines.Any(line => IsSameRequisitionItem(orderedLine, line))))
            {
                throw;
            }

            LogConcurrentReservation(ex, request.OrderId, request.PayloadId);

            return null;
        }
    }

    public virtual async Task ReleaseItemsAsync(IList<PunchoutOrderLine> lines)
    {
        var ids = lines?.Select(x => x.Id).Where(x => !string.IsNullOrEmpty(x)).ToList() ?? [];

        if (ids.Count > 0)
        {
            await orderLineService.DeleteAsync(ids);
        }
    }

    public virtual async Task ReleaseRemovedItemsAsync(PunchoutOrderRequestHandlerContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var lines = await GetPurchaseOrderLinesAsync(context.Configuration.Id, context.Request.OrderId);
        var lineNumbers = context.Request.Items.Select(GetStoredLineNumber).ToHashSet(StringComparer.OrdinalIgnoreCase);

        await ReleaseItemsAsync(lines.Where(line => !lineNumbers.Contains(line.LineNumber ?? string.Empty)).ToList());
    }

    public virtual async Task ReleasePurchaseOrderItemsAsync(PunchoutOrderRequestHandlerContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        await ReleaseItemsAsync(await GetPurchaseOrderLinesAsync(context.Configuration.Id, context.Request.OrderId));
    }

    /// <summary>
    /// The quantity of a line added by an 'update' is checked later, when it is added to the customer order.
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
    /// The order message sends prices with two decimals.
    /// </summary>
    protected virtual decimal RoundPrice(decimal price)
    {
        return Math.Round(price, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// The buyer may return the line item id without the session id.
    /// </summary>
    protected virtual bool IsSameLine(PunchoutOrderLine line, PunchoutOrderRequestItem item)
    {
        return line.LineItemId.EqualsIgnoreCase(item.LineItemId) &&
               (string.IsNullOrEmpty(item.SessionId) || line.SessionId.EqualsIgnoreCase(item.SessionId));
    }

    protected virtual bool IsSameItem(PunchoutRequisitionItem requisitionItem, PunchoutOrderRequestItem item)
    {
        return requisitionItem.LineItemId.EqualsIgnoreCase(item.LineItemId) &&
               (string.IsNullOrEmpty(item.SessionId) || requisitionItem.SessionId.EqualsIgnoreCase(item.SessionId));
    }

    protected virtual bool IsSameRequisitionItem(PunchoutOrderLine line, PunchoutRequisitionItem requisitionItem)
    {
        return line.SessionId.EqualsIgnoreCase(requisitionItem.SessionId) &&
               line.LineItemId.EqualsIgnoreCase(requisitionItem.LineItemId);
    }

    protected virtual bool IsSameRequisitionItem(PunchoutOrderLine line, PunchoutOrderLine otherLine)
    {
        return line.SessionId.EqualsIgnoreCase(otherLine.SessionId) &&
               line.LineItemId.EqualsIgnoreCase(otherLine.LineItemId);
    }

    /// <summary>
    /// The line number truncated as in the stored order line.
    /// </summary>
    protected virtual string GetStoredLineNumber(PunchoutOrderRequestItem item)
    {
        return Truncate(item.LineNumber, ModuleConstants.FieldLength.LineNumber) ?? string.Empty;
    }

    protected virtual PunchoutOrderLine CreateOrderLine(PunchoutOrderRequestHandlerContext context, PunchoutOrderRequestItem item)
    {
        var line = AbstractTypeFactory<PunchoutOrderLine>.TryCreateInstance();

        line.ConfigurationId = context.Configuration.Id;
        line.SessionId = item.RequisitionItem.SessionId;
        line.LineItemId = item.RequisitionItem.LineItemId;
        line.PurchaseOrderNumber = context.Request.OrderId;
        line.LineNumber = Truncate(item.LineNumber, ModuleConstants.FieldLength.LineNumber);
        line.OrderRequestId = context.OrderRequestId;

        return line;
    }

    protected virtual Task<IList<PunchoutOrderLine>> GetPurchaseOrderLinesAsync(string configurationId, string purchaseOrderNumber)
    {
        var criteria = AbstractTypeFactory<PunchoutOrderLineSearchCriteria>.TryCreateInstance();
        criteria.ConfigurationId = configurationId;
        criteria.PurchaseOrderNumber = purchaseOrderNumber;

        return orderLineSearchService.SearchAllNoCloneAsync(criteria);
    }

    /// <summary>
    /// Lines of any purchase order.
    /// </summary>
    protected virtual Task<IList<PunchoutOrderLine>> GetOrderedLinesAsync(string configurationId, IList<string> lineItemIds)
    {
        var criteria = AbstractTypeFactory<PunchoutOrderLineSearchCriteria>.TryCreateInstance();
        criteria.ConfigurationId = configurationId;
        criteria.LineItemIds = lineItemIds;

        return orderLineSearchService.SearchAllNoCloneAsync(criteria);
    }

    protected static string Truncate(string value, int maxLength)
    {
        return value?.Length > maxLength ? value[..maxLength] : value;
    }

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Punchout order request '{OrderId}' with payload '{PayloadId}': a requisition item is ordered by a concurrent purchase order.")]
    private partial void LogConcurrentReservation(Exception ex, string orderId, string payloadId);
}
