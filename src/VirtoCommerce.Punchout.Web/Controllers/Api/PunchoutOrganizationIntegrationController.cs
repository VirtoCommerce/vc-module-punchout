using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using VirtoCommerce.Punchout.Core.Services;
using Permissions = VirtoCommerce.Punchout.Core.ModuleConstants.Security.Permissions;

namespace VirtoCommerce.Punchout.Web.Controllers.Api;

[Authorize]
[Route("api/punchout/organizations/{organizationId}/integrations")]
public class PunchoutOrganizationIntegrationController(IPunchoutOrganizationIntegrationService organizationIntegrationService)
    : Controller
{
    [HttpGet]
    [Authorize(Permissions.Read)]
    public async Task<ActionResult<IList<string>>> GetIntegrationIds([FromRoute] string organizationId)
    {
        var integrationIds = await organizationIntegrationService.GetIntegrationIdsAsync(organizationId);
        return Ok(integrationIds);
    }

    [HttpPut]
    [Authorize(Permissions.Update)]
    [ProducesResponseType(typeof(void), StatusCodes.Status204NoContent)]
    public async Task<ActionResult> SetIntegrations([FromRoute] string organizationId, [FromBody] string[] integrationIds)
    {
        await organizationIntegrationService.SetIntegrationsAsync(organizationId, integrationIds);
        return NoContent();
    }
}
