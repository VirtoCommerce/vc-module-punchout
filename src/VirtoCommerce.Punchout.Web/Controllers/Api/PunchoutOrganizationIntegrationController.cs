using System;
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
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> SetIntegrations([FromRoute] string organizationId, [FromBody] string[] integrationIds)
    {
        try
        {
            await organizationIntegrationService.SetIntegrationsAsync(organizationId, integrationIds);
        }
        // Raised when one of the integrations is already assigned to another organization.
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }

        return NoContent();
    }
}
