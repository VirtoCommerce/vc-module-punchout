using System;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.Punchout.Core.Models;

public class PunchoutSession : AuditableEntity, ICloneable
{
    public string StoreId { get; set; }

    // Supplier (storefront) correlation, SHA-256 hash of the session token
    public string SessionTokenHash { get; set; }

    // One-time redemption flag for the session token (false means the buyer's browser never came to the storefront)
    public bool IsSessionTokenRedeemed { get; set; }

    // Buyer correlation 
    public string BuyerCookie { get; set; }

    public string BuyerIdentity { get; set; }

    public string BuyerDomain { get; set; }

    // System that sent the setup request (cXML Header/Sender/Credential/Identity), used to find the user mapping
    public string SenderIdentity { get; set; }

    public string SenderDomain { get; set; }

    // URL from BrowserFormPost
    public string ReturnUrl { get; set; }

    public DateTime? ExpirationDate { get; set; }

    public DateTime? TokenExpirationDate { get; set; }

    // Active (punchout setup request succeeded)
    // Returned (order created successfully and passed to return url)
    // Expired
    public string Status { get; set; }

    // Start page URL without the session token
    public string StartPage { get; set; }

    public string UserId { get; set; }

    public object Clone()
    {
        return MemberwiseClone();
    }
}
