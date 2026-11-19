using System.Threading.Tasks;
using VirtoCommerce.Punchout.Core.Models;

namespace VirtoCommerce.Punchout.Core.Services;

/// <summary>
/// Extension point for the punchout transactions. The processors authenticate and validate the requests,
/// the handler runs the business logic: the cart, the requisition items and the customer order.
/// </summary>
public interface IPunchoutHandler
{
    /// <summary>
    /// Processes the setup request
    /// </summary>
    Task HandleSetupAsync(PunchoutSetupHandlerContext context);

    /// <summary>
    /// Builds the cXML PunchOutOrderMessage returned to the buyer and the snapshot of the sent requisition items
    /// </summary>
    Task HandleOrderMessageAsync(PunchoutOrderMessageHandlerContext context);

    /// <summary>
    /// Creates, updates or cancels the customer order of the purchase order
    /// </summary>
    Task HandleOrderRequestAsync(PunchoutOrderRequestHandlerContext context);
}
