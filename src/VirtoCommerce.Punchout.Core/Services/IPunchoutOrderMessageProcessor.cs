using System.Threading.Tasks;
using VirtoCommerce.Punchout.Core.Models;

namespace VirtoCommerce.Punchout.Core.Services;

/// <summary>
/// Validates the punchout session and lets the configuration handler build the PunchOutOrderMessage.
/// </summary>
public interface IPunchoutOrderMessageProcessor
{
    Task<PunchoutCheckoutResult> ProcessAsync(PunchoutOrderMessageRequest request);
}
