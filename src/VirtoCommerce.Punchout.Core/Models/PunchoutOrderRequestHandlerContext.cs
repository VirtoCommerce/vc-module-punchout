namespace VirtoCommerce.Punchout.Core.Models;

public class PunchoutOrderRequestHandlerContext : PunchoutHandlerContext
{
    public PunchoutOrderRequestData Request { get; set; }

    public PunchoutUserMapping UserMapping { get; set; }

    /// <summary>
    /// The current version of the purchase order changed by an 'update' or 'delete', null for a 'new' order.
    /// An 'update' without a processed previous version has no previous request and is created as a new order.
    /// </summary>
    public PunchoutOrderRequest PreviousRequest { get; set; }

    public PunchoutOrderResult Result { get; set; }
}
