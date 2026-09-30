using System.Threading.Tasks;
using VirtoCommerce.Punchout.Core.Models;

namespace VirtoCommerce.Punchout.Core.Services;

/// <summary>
/// Extension point for the punchout transactions.
/// </summary>
public interface IPunchoutHandler
{
    Task HandleSetupAsync(PunchoutSetupHandlerContext context);
}
