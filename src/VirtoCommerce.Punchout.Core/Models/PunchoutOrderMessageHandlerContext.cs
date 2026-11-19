using System.Collections.Generic;

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

    /// <summary>
    /// The handler sets the snapshot of the items sent in the message, it is stored with the message.
    /// </summary>
    public IList<PunchoutRequisitionItem> Items { get; set; } = [];

    /// <summary>
    /// The cart the items are sent from, the handler removes them from it after the session is returned.
    /// </summary>
    public string CartId { get; set; }
}
