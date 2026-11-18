using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.Punchout.Core.Models;

namespace VirtoCommerce.Punchout.Core.Services;

/// <summary>
/// Tracks the requisition items ordered by the purchase orders.
/// </summary>
public interface IPunchoutOrderLineService
{
    /// <summary>
    /// The ordered items with the given cart line item ids.
    /// </summary>
    Task<IList<PunchoutOrderLine>> GetByLineItemIdsAsync(string configurationId, IList<string> lineItemIds);

    /// <summary>
    /// The items ordered by the purchase order.
    /// </summary>
    Task<IList<PunchoutOrderLine>> GetByPurchaseOrderAsync(string configurationId, string purchaseOrderNumber);

    /// <summary>
    /// Stores all the lines, or none of them if an item is already ordered.
    /// </summary>
    /// <returns>False if an item is already ordered, e.g. by a concurrent request</returns>
    Task<bool> TryAddAsync(IList<PunchoutOrderLine> lines);

    Task DeleteAsync(IList<string> ids);
}
