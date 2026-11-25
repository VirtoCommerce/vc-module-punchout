using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.Punchout.Core.Services;

namespace VirtoCommerce.Punchout.Data.Services;

/// <summary>
/// Default policy allows only non-administrator accounts of Customer type.
/// </summary>
public class PunchoutUserPolicy : IPunchoutUserPolicy
{
    public virtual bool IsAllowed(ApplicationUser user)
    {
        return user != null &&
               !user.IsAdministrator &&
               user.UserType == nameof(UserType.Customer);
    }
}
