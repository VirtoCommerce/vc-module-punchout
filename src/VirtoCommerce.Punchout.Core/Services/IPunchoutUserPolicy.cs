using VirtoCommerce.Platform.Core.Security;

namespace VirtoCommerce.Punchout.Core.Services;

/// <summary>
/// Used both when a user mapping is saved and when a user of a mapped organization is resolved.
/// </summary>
public interface IPunchoutUserPolicy
{
    bool IsAllowed(ApplicationUser user);
}
