using System.Collections.Generic;
using System.Threading.Tasks;

namespace VirtoCommerce.Punchout.Core.Services;

/// <summary>
/// Manages the links between organizations and punchout integrations from the organization side.
/// </summary>
public interface IPunchoutOrganizationIntegrationService
{
    /// <summary>
    /// Returns the IDs of the integrations linked to the organization.
    /// </summary>
    Task<IList<string>> GetIntegrationIdsAsync(string organizationId);

    /// <summary>
    /// Makes the organization linked to exactly these integrations: links that are missing are added,
    /// links that are not listed are removed. Integrations not affected by the change are left untouched.
    /// </summary>
    Task SetIntegrationsAsync(string organizationId, IList<string> integrationIds);
}
