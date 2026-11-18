using System.Threading.Tasks;
using VirtoCommerce.Punchout.Core.Models;

namespace VirtoCommerce.Punchout.Core.Services;

public interface IPunchoutSetupProcessor
{
    Task<PunchoutSetupResult> ProcessAsync(PunchoutSetupRequest request);
}
