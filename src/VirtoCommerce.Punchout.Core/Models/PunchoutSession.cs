using System;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.Punchout.Core.Models;

public class PunchoutSession : AuditableEntity, ICloneable
{
    public string StoreId { get; set; }

    // Supplier (storefront) correlation
    public string SessionToken { get; set; }

    // Buyer correlation 
    public string BuyerCookie { get; set; }

    public string BuyerIdentity { get; set; }

    public string BuyerDomain { get; set; }

    // URL from BrowserFormPost
    public string ReturnUrl { get; set; }

    public DateTime? ExpirationDate { get; set; }

    // Created
    // Active (user successfully opened and validated store url)
    // Returned (order created successfully and passed to return url)
    // Expired
    public string Status { get; set; }

    public string StartPage { get; set; }

    public string UserId { get; set; }

    public object Clone()
    {
        return MemberwiseClone();
    }
}
