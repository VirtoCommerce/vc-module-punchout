using System;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.Punchout.Core.Models;

public class PunchoutSession : AuditableEntity, ICloneable
{
    public string StoreId { get; set; }

    // In case we need to hold multiple carts, null means we use the default cart 
    public string CartId { get; set; }

    // Buyer correlration 
    public string BuyerCookie { get; set; }

    // Supplier (storefront) correlation
    public string SesssionToken { get; set; }

    // URL from BrowserFormPost
    public string ReturnUrl { get; set; }

    public string BuyerIdentity { get; set; }

    public string BuyerDomain { get; set; }

    public DateTime? ExpirationDate { get; set; }

    // Active, Returned, Expired, Cancelled
    public string Status { get; set; }

    public object Clone()
    {
        return MemberwiseClone();
    }
}
