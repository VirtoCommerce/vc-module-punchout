using System;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.Punchout.Core.Models;

public class PunchoutSession : AuditableEntity, ICloneable
{
    public string StoreId { get; set; }

    // In case we need to hold multiple carts, NULL means we use the default cart (use NULL case for now) 
    public string CartId { get; set; }

    // Supplier (storefront) correlation
    public string SessionToken { get; set; }

    // Buyer correlration 
    public string BuyerCookie { get; set; }

    public string BuyerIdentity { get; set; }

    public string BuyerDomain { get; set; }

    // URL from BrowserFormPost
    public string ReturnUrl { get; set; }

    public DateTime? ExpirationDate { get; set; }

    // Created
    // Active (user successfuly opened and validated store url)
    // Returned (order created successfuly and passed to return url)
    // Expired
    // Cancelled (maybe?)
    public string Status { get; set; }

    public string StartPage { get; set; }

    public string IntegrationId { get; set; }

    public object Clone()
    {
        return MemberwiseClone();
    }
}
