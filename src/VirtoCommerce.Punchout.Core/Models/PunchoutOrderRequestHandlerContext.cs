namespace VirtoCommerce.Punchout.Core.Models;

public class PunchoutOrderRequestHandlerContext : PunchoutHandlerContext
{
    public PunchoutOrderRequestData Request { get; set; }

    /// <summary>
    /// The id of the stored order request, the reserved requisition items reference it.
    /// </summary>
    public string OrderRequestId { get; set; }

    public PunchoutUserMapping UserMapping { get; set; }

    /// <summary>
    /// The current version changed by an 'update' or 'delete'. Null for a 'new' order and for an 'update' created as a new order.
    /// </summary>
    public PunchoutOrderRequest PreviousRequest { get; set; }

    public PunchoutOrderResult Result { get; set; }
}
