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
    /// The customer of a new order: the mapped user, or the end user resolved in the mapped organization.
    /// An 'update' or 'delete' keeps the customer of the existing order.
    /// </summary>
    public PunchoutOrderCustomer Customer { get; set; }

    /// <summary>
    /// The current version changed by an 'update' or 'delete'. Null for a 'new' order and for an 'update' created as a new order.
    /// </summary>
    public PunchoutOrderRequest PreviousRequest { get; set; }

    public PunchoutOrderResult Result { get; set; }
}
