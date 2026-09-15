using System;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.Punchout.Core.Models;

public class PunchoutSession : AuditableEntity, ICloneable
{
    public string StoreId { get; set; }

    // Created and filled while activating punchout session
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

    /// <summary>
    /// The platform user the session belongs to, resolved from the sender identity of the setup request
    /// through <see cref="PunchoutUserMapping"/>.
    /// </summary>
    public string UserId { get; set; }

    public object Clone()
    {
        return MemberwiseClone();
    }
}
