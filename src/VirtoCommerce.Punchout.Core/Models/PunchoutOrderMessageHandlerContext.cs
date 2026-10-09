namespace VirtoCommerce.Punchout.Core.Models;

public class PunchoutOrderMessageHandlerContext : PunchoutHandlerContext
{
    /// <summary>
    /// The request with the store defaults applied to the culture and currency.
    /// </summary>
    public PunchoutOrderMessageRequest Request { get; set; }

    public PunchoutSession Session { get; set; }

    /// <summary>
    /// Prefilled with the return URL and the form field, the handler sets the cXML.
    /// </summary>
    public PunchoutCheckoutResult Result { get; set; }
}
