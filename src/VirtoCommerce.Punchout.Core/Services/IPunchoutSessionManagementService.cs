using System.Threading.Tasks;
using VirtoCommerce.Punchout.Core.Models;

namespace VirtoCommerce.Punchout.Core.Services;

public interface IPunchoutSessionManagementService
{
    Task<ReedeemPunchoutSessionResult> RedeemSessionAsync(ReedeemPunchoutSessionRequest request);
}
