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
}
