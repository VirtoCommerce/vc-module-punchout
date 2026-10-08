using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.Punchout.Core.Models;
using VirtoCommerce.Punchout.Core.Services;
using Permissions = VirtoCommerce.Punchout.Core.ModuleConstants.Security.Permissions;

namespace VirtoCommerce.Punchout.Web.Controllers.Api;

[Authorize]
[Route("api/punchout-user-mappings")]
public class PunchoutUserMappingController(
    IPunchoutUserMappingService crudService,
    IPunchoutUserMappingSearchService searchService,
    UserManager<ApplicationUser> userManager)
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
        var error = await ValidateExternalIdAsync(model) ?? await ValidateUserAsync(model);
        if (error != null)
        {
            return BadRequest(error);
        }

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

    private async Task<string> ValidateExternalIdAsync(PunchoutUserMapping model)
    {
        if (model.ExternalId.IsNullOrEmpty())
        {
            return "ExternalId is required.";
        }

        var criteria = AbstractTypeFactory<PunchoutUserMappingSearchCriteria>.TryCreateInstance();
        criteria.ExternalIds = [model.ExternalId];
        criteria.Take = 1;

        var existing = await searchService.SearchNoCloneAsync(criteria);
        if (existing.Results.Any(x => x.Id != model.Id))
        {
            return $"A mapping with ExternalId '{model.ExternalId}' already exists.";
        }

        return null;
    }

    private async Task<string> ValidateUserAsync(PunchoutUserMapping model)
    {
        if (model.UserId.IsNullOrEmpty() || model.MemberId.IsNullOrEmpty())
        {
            return "UserId and MemberId are required.";
        }

        var user = await userManager.FindByIdAsync(model.UserId);
        if (user == null || user.MemberId != model.MemberId)
        {
            return "The user is not a security account of the specified contact.";
        }

        if (user.IsAdministrator || user.UserType != nameof(UserType.Customer))
        {
            return "Only non-administrator customer accounts can be mapped.";
        }

        return null;
    }
}
