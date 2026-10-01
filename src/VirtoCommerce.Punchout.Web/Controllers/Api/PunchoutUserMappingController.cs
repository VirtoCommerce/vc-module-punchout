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
[Route("api/punchout-user-mappings")]
public class PunchoutUserMappingController(
    IPunchoutUserMappingService crudService,
    IPunchoutUserMappingSearchService searchService)
    : Controller
{
    [HttpPost("search")]
    [Authorize(Permissions.Read)]
    public async Task<ActionResult<PunchoutUserMappingSearchResult>> Search([FromBody] PunchoutUserMappingSearchCriteria criteria)
    {
        var result = await searchService.SearchAsync(criteria);
        return Ok(result);
    }

    [HttpGet("{id}")]
    [Authorize(Permissions.Read)]
    public async Task<ActionResult<PunchoutUserMapping>> Get([FromRoute] string id, [FromQuery] string responseGroup = null)
    {
        var model = await crudService.GetByIdAsync(id, responseGroup);
        return Ok(model);
    }

    [HttpGet("new")]
    [Authorize(Permissions.Read)]
    public ActionResult<PunchoutUserMapping> GetNew()
    {
        var model = AbstractTypeFactory<PunchoutUserMapping>.TryCreateInstance();

        model.IsActive = true;

        return Ok(model);
    }

    [HttpPost]
    [Authorize(Permissions.Create)]
    public Task<ActionResult<PunchoutUserMapping>> Create([FromBody] PunchoutUserMapping model)
    {
        model.Id = null;
        return Update(model);
    }

    [HttpPut]
    [Authorize(Permissions.Update)]
    public async Task<ActionResult<PunchoutUserMapping>> Update([FromBody] PunchoutUserMapping model)
    {
        await crudService.SaveChangesAsync([model]);
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
