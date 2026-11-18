using System.Collections.Generic;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.Punchout.Core.Models;

public class PunchoutOrderRequestSearchCriteria : SearchCriteriaBase
{
    public string ConfigurationId { get; set; }

    public string StoreId { get; set; }

    public string PayloadId { get; set; }

    public string PurchaseOrderNumber { get; set; }

    public string OrderType { get; set; }

    public string CustomerOrderId { get; set; }

    public IList<string> Statuses { get; set; }
}
