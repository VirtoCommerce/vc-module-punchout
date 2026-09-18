using System.Collections.Generic;

namespace VirtoCommerce.Punchout.Core.Models;

public class PunchoutSetupContext
{
    public string BuyerCookie { get; set; }

    public string ReturnUrl { get; set; }

    /// <summary>
    /// Identity of the buying organization (cXML Header/From/Credential/Identity).
    /// </summary>
    public string From { get; set; }

    public string FromDomain { get; set; }

    /// <summary>
    /// Identity of the supplier the request is addressed to (cXML Header/To/Credential/Identity).
    /// </summary>
    public string To { get; set; }

    public string ToDomain { get; set; }

    /// <summary>
    /// Identity of the system that sent the request and owns the shared secret (cXML Header/Sender/Credential/Identity)
    /// </summary>
    public string Sender { get; set; }

    public string SenderDomain { get; set; }

    public string SharedSecret { get; set; }

    public IDictionary<string, string> Extrinsics { get; set; }

    /// <summary>
    /// The person who started the session. Null when the request carries no usable Contact.
    /// </summary>
    public PunchoutUserContext User { get; set; }
}
