namespace VirtoCommerce.Punchout.Core.Models;

public abstract class BasePunchoutRequest
{
    /// <summary>
    /// Unique id of the cXML document, can be used to detect duplicate submissions.
    /// </summary>
    public string PayloadId { get; set; }

    /// <summary>
    /// Buying organization (cXML Header/From/Credential/Identity).
    /// </summary>
    public string From { get; set; }

    public string FromDomain { get; set; }

    /// <summary>
    /// Supplier the request is addressed to (cXML Header/To/Credential/Identity).
    /// </summary>
    public string To { get; set; }

    public string ToDomain { get; set; }

    /// <summary>
    /// System that sent the request and the shared secret (cXML Header/Sender/Credential/Identity)
    /// </summary>
    public string Sender { get; set; }

    public string SenderDomain { get; set; }

    public string SharedSecret { get; set; }
}
