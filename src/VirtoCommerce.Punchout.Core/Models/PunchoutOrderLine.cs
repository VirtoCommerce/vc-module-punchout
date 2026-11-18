using System;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.Punchout.Core.Models;

/// <summary>
/// A requisition item (a line of a PunchOutOrderMessage) ordered by a purchase order.
/// The buyer may split the items of one requisition between purchase orders, but an item is ordered only once.
/// </summary>
public class PunchoutOrderLine : AuditableEntity, ICloneable
{
    public string ConfigurationId { get; set; }

    /// <summary>
    /// The session that returned the requisition item.
    /// </summary>
    public string SessionId { get; set; }

    /// <summary>
    /// The cart line item id of the requisition item.
    /// </summary>
    public string LineItemId { get; set; }

    /// <summary>
    /// The purchase order (orderID) that orders the item.
    /// </summary>
    public string PurchaseOrderNumber { get; set; }

    public string LineNumber { get; set; }

    /// <summary>
    /// The order request that added the item to the purchase order.
    /// </summary>
    public string OrderRequestId { get; set; }

    public virtual object Clone()
    {
        return MemberwiseClone();
    }
}
