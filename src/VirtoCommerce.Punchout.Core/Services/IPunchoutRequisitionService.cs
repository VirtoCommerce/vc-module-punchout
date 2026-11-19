using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.Punchout.Core.Models;

namespace VirtoCommerce.Punchout.Core.Services;

/// <summary>
/// Matches the purchase order lines with the requisition items returned to the buyer, and reserves the requisition items
/// for the purchase order, so one requisition item is ordered by one purchase order only.
/// </summary>
public interface IPunchoutRequisitionService
{
    /// <summary>
    /// Matches every order line with the requisition item it orders and sets <see cref="PunchoutOrderRequestItem.RequisitionItem"/>.
    /// A line of an 'update' (<paramref name="isNewOrder"/> is false) whose line number is already ordered by the purchase order is not matched,
    /// it must order the same requisition item as before. Returns the error message, or null when the order matches the requisitions.
    /// </summary>
    Task<string> MatchItemsAsync(PunchoutOrderRequestHandlerContext context, bool isNewOrder);

    /// <summary>
    /// Reserves the matched requisition items that are not ordered by the purchase order yet.
    /// Returns the added lines, or null when an item is ordered by a concurrent purchase order.
    /// </summary>
    Task<IList<PunchoutOrderLine>> ReserveItemsAsync(PunchoutOrderRequestHandlerContext context);

    /// <summary>
    /// Releases the reserved lines, e.g. when the customer order was not created.
    /// </summary>
    Task ReleaseItemsAsync(IList<PunchoutOrderLine> lines);

    /// <summary>
    /// Releases the items of the purchase order whose line numbers are missing from the processed 'update'.
    /// </summary>
    Task ReleaseRemovedItemsAsync(PunchoutOrderRequestHandlerContext context);

    /// <summary>
    /// Releases all the items of the purchase order, e.g. after the customer order is cancelled.
    /// </summary>
    Task ReleasePurchaseOrderItemsAsync(PunchoutOrderRequestHandlerContext context);
}
