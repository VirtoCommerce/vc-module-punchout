using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Permissions = VirtoCommerce.Punchout.Core.ModuleConstants.Security.Permissions;

namespace VirtoCommerce.Punchout.Web.Controllers.Api;

[Authorize]
[Route("api/punchout/cxml")]
public class PunchoutController : Controller
{
    public PunchoutController()
    {

    }

    // POST: api/punchout/cxml/setup
    [HttpGet]
    [Route("setup")]
    [Authorize(Permissions.Read)]
    public async Task<ActionResult> Setup()
    {
        return Ok(new { result = "OK" });
    }
}
