using System;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.Punchout.Core.Models;

public class PunchoutOrderRequest : AuditableEntity, ICloneable
{
    public string ConfigurationId { get; set; }

    public string StoreId { get; set; }

    public string PayloadId { get; set; }

    public string SenderIdentity { get; set; }

    public string BuyerIdentity { get; set; }

    public string UserId { get; set; }

    // new, update, delete 
    public string OrderType { get; set; }

    public string PurchaseOrderNumber { get; set; }

    public DateTime? PurchaseOrderDate { get; set; }

    public string Currency { get; set; }

    public decimal? Total { get; set; }

    public string CustomerOrderId { get; set; }

    // Processing, Completed, Failed, Rejected
    public string Status { get; set; }

    public string ErrorCode { get; set; }

    public string ErrorMessage { get; set; }

    public virtual object Clone()
    {
        return MemberwiseClone();
    }
}
