using System.Threading.Tasks;
using VirtoCommerce.Punchout.Core.Models;
using VirtoCommerce.Punchout.Core.Services;

namespace VirtoCommerce.Punchout.Data.Services;

/// <summary>
/// Default handler, does nothing. Override it to customize a punchout transaction.
/// </summary>
public class DefaultPunchoutHandler : IPunchoutHandler
{
    public virtual Task HandleSetupAsync(PunchoutSetupHandlerContext context)
    {
        return Task.CompletedTask;
    }
}
