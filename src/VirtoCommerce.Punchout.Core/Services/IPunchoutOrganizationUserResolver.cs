using System.Threading.Tasks;
using VirtoCommerce.Punchout.Core.Models;

namespace VirtoCommerce.Punchout.Core.Services;

/// <summary>
/// Resolves the existing user of an organization a procurement system identity is mapped to.
/// </summary>
public interface IPunchoutOrganizationUserResolver
{
    Task<PunchoutOrganizationUserResult> ResolveAsync(PunchoutOrganizationUserRequest request);
}
