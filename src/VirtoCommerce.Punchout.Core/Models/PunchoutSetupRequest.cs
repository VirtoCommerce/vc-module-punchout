using System.Collections.Generic;

namespace VirtoCommerce.Punchout.Core.Models;

public class PunchoutSetupRequest : BasePunchoutRequest
{
    public string BuyerCookie { get; set; }

    public string ReturnUrl { get; set; }

    public IDictionary<string, string> Extrinsics { get; set; }

    /// <summary>
    /// Null when the request carries no usable Contact.
    /// </summary>
    public PunchoutUserContext User { get; set; }
}
