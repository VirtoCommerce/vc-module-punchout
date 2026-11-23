using System.Collections.Generic;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.Punchout.Core.Models;

public class PunchoutOrderLineSearchCriteria : SearchCriteriaBase
{
    public string ConfigurationId { get; set; }

    public string PurchaseOrderNumber { get; set; }

    /// <summary>
    /// The cart line item ids of the requisition items.
    /// </summary>
    public IList<string> LineItemIds { get; set; }
}
