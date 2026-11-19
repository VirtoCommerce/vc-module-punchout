using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.Punchout.Core.Models;

namespace VirtoCommerce.Punchout.Core.Services;

/// <summary>
/// Matches purchase order lines with the requisition items returned to the buyer and reserves them,
/// so a requisition item is ordered by one purchase order only.
/// </summary>
public interface IPunchoutRequisitionService
{
    /// <summary>
    /// Sets <see cref="PunchoutOrderRequestItem.RequisitionItem"/> for every line. A line without a session id is searched in all requisitions of the user.
    /// On 'update', a line number already in the purchase order must keep its requisition item. Returns the error message, or null.
    /// </summary>
    Task<string> MatchItemsAsync(PunchoutOrderRequestHandlerContext context, bool isNewOrder);

    /// <summary>
    /// Reserves the matched items not yet in the purchase order. Returns null when a concurrent purchase order took an item.
    /// </summary>
    Task<IList<PunchoutOrderLine>> ReserveItemsAsync(PunchoutOrderRequestHandlerContext context);

    Task ReleaseItemsAsync(IList<PunchoutOrderLine> lines);

    /// <summary>
    /// Releases the items whose line numbers are missing from the 'update'.
    /// </summary>
    Task ReleaseRemovedItemsAsync(PunchoutOrderRequestHandlerContext context);

    Task ReleasePurchaseOrderItemsAsync(PunchoutOrderRequestHandlerContext context);
}
