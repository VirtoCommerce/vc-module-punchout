using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.Punchout.Core.Models;

namespace VirtoCommerce.Punchout.Core.Services;

/// <summary>
/// Finds the requisition items sent to the buyers in the PunchOutOrderMessages.
/// </summary>
public interface IPunchoutRequisitionItemService
{
    /// <summary>
    /// The requisition items with the given cart line item ids, returned by the sessions of the user and the configuration.
    /// One cart line item may be in the requisitions of several sessions, e.g. when it was not removed from the cart after the first one.
    /// </summary>
    Task<IList<PunchoutRequisitionItem>> GetReturnedItemsAsync(string configurationId, string userId, IList<string> lineItemIds);
}
