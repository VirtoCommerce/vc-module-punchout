using System.Threading.Tasks;
using VirtoCommerce.Punchout.Core.Models;

namespace VirtoCommerce.Punchout.Core.Services;

/// <summary>
/// Extension point for the punchout transactions.
/// </summary>
public interface IPunchoutHandler
{
    /// <summary>
    /// Processes the setup request
    /// </summary>
    Task HandleSetupAsync(PunchoutSetupHandlerContext context);

    /// <summary>
    /// Builds the cXML PunchOutOrderMessage returned to the buyer
    /// </summary>
    Task HandleOrderMessageAsync(PunchoutOrderMessageHandlerContext context);

    /// <summary>
    /// Called after the session is returned and the order message is stored, e.g. to remove the sent items from the cart,
    /// so the next punchout session does not send them again.
    /// </summary>
    Task HandleOrderMessageReturnedAsync(PunchoutOrderMessageHandlerContext context) => Task.CompletedTask;

    /// <summary>
    /// Processes the order request
    /// </summary>
    Task HandleOrderRequestAsync(PunchoutOrderRequestHandlerContext context);
}
