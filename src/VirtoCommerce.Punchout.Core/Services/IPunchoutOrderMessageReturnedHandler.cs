using System.Threading.Tasks;
using VirtoCommerce.Punchout.Core.Models;

namespace VirtoCommerce.Punchout.Core.Services;

/// <summary>
/// Optional interface of an <see cref="IPunchoutHandler"/>. It is called after the session is returned and the order message is stored,
/// e.g. to remove the sent items from the cart, so the next punchout session does not send them again.
/// The requisition is already returned to the buyer, a failure is logged and does not change the result.
/// </summary>
public interface IPunchoutOrderMessageReturnedHandler
{
    Task HandleOrderMessageReturnedAsync(PunchoutOrderMessageHandlerContext context);
}
