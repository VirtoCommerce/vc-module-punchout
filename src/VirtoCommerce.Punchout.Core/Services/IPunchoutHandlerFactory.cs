using VirtoCommerce.Punchout.Core.Models;

namespace VirtoCommerce.Punchout.Core.Services;

/// <summary>
/// Resolves the punchout handler of a configuration.
/// </summary>
public interface IPunchoutHandlerFactory
{
    IPunchoutHandler Create(PunchoutConfiguration configuration);
}
