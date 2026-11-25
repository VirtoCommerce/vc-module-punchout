using System.Threading.Tasks;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.Punchout.Core.Models;

namespace VirtoCommerce.Punchout.Core.Services;

/// <summary>
/// Resolves the existing user of an organization a procurement system identity is mapped to.
/// </summary>
public interface IPunchoutOrganizationUserResolver
{
    Task<PunchoutOrganizationUserResult> ResolveAsync(PunchoutOrganizationUserRequest request);

    /// <summary>
    /// Checks that the contact of the user belongs to the organization.
    /// </summary>
    Task<bool> IsOrganizationUserAsync(ApplicationUser user, string organizationId);
}
