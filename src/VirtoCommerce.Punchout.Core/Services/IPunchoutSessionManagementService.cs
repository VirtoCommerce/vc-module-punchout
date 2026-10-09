using System.Threading.Tasks;
using VirtoCommerce.Punchout.Core.Models;

namespace VirtoCommerce.Punchout.Core.Services;

public interface IPunchoutSessionManagementService
{
    Task<RedeemPunchoutSessionResult> RedeemSessionAsync(RedeemPunchoutSessionRequest request);

    /// <summary>
    /// Marks the session as Returned to the procurement system, so it cannot transfer a requisition again.
    /// </summary>
    /// <returns>True if the session was active and has been returned by this call</returns>
    Task<bool> ReturnSessionAsync(string sessionId);
}
