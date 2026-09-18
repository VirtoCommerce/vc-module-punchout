using System.Threading.Tasks;
using VirtoCommerce.Punchout.Core.Models;

namespace VirtoCommerce.Punchout.Core.Services;

public interface IPunchoutSetupService
{
    Task<PunchoutSetupResult> ProcessAsync(PunchoutSetupContext punchoutSetupContext);
}
