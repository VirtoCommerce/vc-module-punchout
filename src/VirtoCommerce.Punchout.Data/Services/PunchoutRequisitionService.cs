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

public class PunchoutRequisitionService(
    IPunchoutRequisitionItemService requisitionItemService,
    IPunchoutOrderLineService orderLineService,
    IPunchoutOrderLineSearchService orderLineSearchService,
    ILogger<PunchoutRequisitionService> logger)
    : IPunchoutRequisitionService
{
    protected const string PriceFormat = "0.00";

    /// <summary>
    /// The items of one purchase order may come from several sessions, each line references its session in the SupplierPartAuxiliaryID.
    /// A line without the session id is matched by the line item id among all the requisitions returned to the user, it must be in one of them.
    /// A requisition item ordered by another purchase order is rejected. A line of an 'update' whose line number is already ordered by the purchase order
    /// is not matched, the customer order line is compared with it. It must order the same requisition item as before.
    /// </summary>
    public virtual async Task<string> MatchItemsAsync(PunchoutOrderRequestHandlerContext context, bool isNewOrder)
    {
        ArgumentNullException.ThrowIfNull(context);

        var request = context.Request;
        var configurationId = context.Configuration.Id;
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
        var orderedLines = await GetOrderedLinesAsync(configurationId, lineItemIds);
        var requisitionItems = await requisitionItemService.GetReturnedItemsAsync(configurationId, context.UserMapping.UserId, lineItemIds);
        var purchaseOrderLines = isNewOrder ? [] : await GetPurchaseOrderLinesAsync(configurationId, request.OrderId);

        foreach (var item in request.Items)
        {
            item.RequisitionItem = null;

            var orderedLine = orderedLines.FirstOrDefault(x => IsSameLine(x, item));

            if (orderedLine is not null && !orderedLine.PurchaseOrderNumber.EqualsIgnoreCase(request.OrderId))
            {
                errors.Add($"Line {item.LineNumber}: item '{item.SupplierPartId}' is already ordered by purchase order '{orderedLine.PurchaseOrderNumber}'.");
                continue;
            }

            if (!isNewOrder)
            {
                // The customer order lines are matched by the line number, an existing line is found the same way
                var purchaseOrderLine = purchaseOrderLines.FirstOrDefault(x => x.LineNumber.EqualsIgnoreCase(GetStoredLineNumber(item)));

                if (purchaseOrderLine is not null)
                {
                    if (!IsSameLine(purchaseOrderLine, item))
                    {
                        errors.Add($"Line {item.LineNumber}: the line orders another requisition item, an update cannot replace the item of a line.");
                    }

                    continue;
                }

                if (orderedLine is not null)
                {
                    errors.Add($"Line {item.LineNumber}: item '{item.SupplierPartId}' is already ordered by line {orderedLine.LineNumber} of the purchase order.");
                    continue;
                }
            }

            var candidates = requisitionItems.Where(x => IsSameItem(x, item)).ToList();

            if (candidates.Count > 1)
            {
                errors.Add($"Line {item.LineNumber}: item '{item.SupplierPartId}' is in several punchout requisitions, SupplierPartAuxiliaryID '{item.SupplierPartAuxiliaryId}' has no session id.");
                continue;
            }

            var requisitionItem = candidates.FirstOrDefault();

            var error = requisitionItem is null
                ? $"Line {item.LineNumber}: item '{item.SupplierPartId}' is not in a punchout requisition returned to the user."
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
    /// Reserves the requisition items for the purchase order before the order is created, so a concurrent purchase order cannot order them.
    /// </summary>
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
            // The unique index rejects an item ordered by a concurrent purchase order, nothing is stored
            var orderedLines = await GetOrderedLinesAsync(configurationId, lines.Select(x => x.LineItemId).ToList());

            if (!orderedLines.Any(orderedLine => lines.Any(line => IsSameRequisitionItem(orderedLine, line))))
            {
                throw;
            }

            logger.LogWarning(ex, "Punchout order request '{OrderId}' with payload '{PayloadId}': a requisition item is ordered by a concurrent purchase order.",
                request.OrderId, request.PayloadId);

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

    /// <summary>
    /// The customer order lines whose line numbers are missing from the update are removed, their items are released.
    /// </summary>
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
    /// Compares an order line with the requisition item sent to the buyer. The quantity of a new order must match,
    /// the quantity of a line added by an 'update' is compared when it is added to the customer order. Returns the mismatch or null.
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
    /// The buyer may return the line item id without the session id, then the line item id is compared only.
    /// </summary>
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
    /// The line number as it is stored in the ordered line.
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

    /// <summary>
    /// The items ordered by the purchase order.
    /// </summary>
    protected virtual Task<IList<PunchoutOrderLine>> GetPurchaseOrderLinesAsync(string configurationId, string purchaseOrderNumber)
    {
        var criteria = AbstractTypeFactory<PunchoutOrderLineSearchCriteria>.TryCreateInstance();
        criteria.ConfigurationId = configurationId;
        criteria.PurchaseOrderNumber = purchaseOrderNumber;

        return orderLineSearchService.SearchAllNoCloneAsync(criteria);
    }

    /// <summary>
    /// The ordered items with the given cart line item ids, by any purchase order.
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
}
