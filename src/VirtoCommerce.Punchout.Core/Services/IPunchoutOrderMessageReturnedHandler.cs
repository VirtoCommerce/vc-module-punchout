using System.Threading.Tasks;
using VirtoCommerce.Punchout.Core.Models;

namespace VirtoCommerce.Punchout.Core.Services;

/// <summary>
/// Optional interface of an <see cref="IPunchoutHandler"/>, called after the order message is stored,
/// e.g. to remove the sent items from the cart. A failure is logged and does not change the result.
/// </summary>
public interface IPunchoutOrderMessageReturnedHandler
{
    Task HandleOrderMessageReturnedAsync(PunchoutOrderMessageHandlerContext context);
}
