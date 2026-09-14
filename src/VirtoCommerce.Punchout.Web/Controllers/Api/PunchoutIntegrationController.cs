using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Punchout.Core.Models;
using VirtoCommerce.Punchout.Core.Services;
using Permissions = VirtoCommerce.Punchout.Core.ModuleConstants.Security.Permissions;

namespace VirtoCommerce.Punchout.Web.Controllers.Api;

[Authorize]
[Route("api/punchout-integrations")]
public class PunchoutIntegrationController(
    IPunchoutIntegrationService crudService,
    IPunchoutIntegrationSearchService searchService)
    : Controller
{
    [HttpPost("search")]
    [Authorize(Permissions.Read)]
    public async Task<ActionResult<PunchoutIntegrationSearchResult>> Search([FromBody] PunchoutIntegrationSearchCriteria criteria)
    {
        var result = await searchService.SearchNoCloneAsync(criteria);
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Permissions.Create)]
    public Task<ActionResult<PunchoutIntegration>> Create([FromBody] PunchoutIntegration model)
    {
        model.Id = null;
        return Update(model);
    }

    [HttpPut]
    [Authorize(Permissions.Update)]
    public async Task<ActionResult<PunchoutIntegration>> Update([FromBody] PunchoutIntegration model)
    {
        await crudService.SaveChangesAsync([model]);
        return Ok(model);
    }

    [HttpGet("{id}")]
    [Authorize(Permissions.Read)]
    public async Task<ActionResult<PunchoutIntegration>> Get([FromRoute] string id, [FromQuery] string responseGroup = null)
    {
        var model = await crudService.GetNoCloneAsync(id, responseGroup);
        return Ok(model);
    }


    [HttpGet("new")]
    [Authorize(Permissions.Read)]
    public async Task<ActionResult<PunchoutIntegration>> GetNew()
    {
        var model = AbstractTypeFactory<PunchoutIntegration>.TryCreateInstance();

        model.SharedSecret = Guid.NewGuid().ToString("N");

        return Ok(model);
    }

    [HttpDelete]
    [Authorize(Permissions.Delete)]
    [ProducesResponseType(typeof(void), StatusCodes.Status204NoContent)]
    public async Task<ActionResult> Delete([FromQuery] string[] ids)
    {
        await crudService.DeleteAsync(ids);
        return NoContent();
    }
}
