using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Permissions = VirtoCommerce.Punchout.Core.ModuleConstants.Security.Permissions;

namespace VirtoCommerce.Punchout.Web.Controllers.Api;

[Authorize]
[Route("api/punchout")]
public class PunchoutController : Controller
{
    // GET: api/punchout
    /// <summary>
    /// Get message
    /// </summary>
    /// <remarks>Return "Hello world!" message</remarks>
    [HttpGet]
    [Route("")]
    [Authorize(Permissions.Read)]
    public ActionResult<string> Get()
    {
        return Ok(new { result = "Hello world!" });
    }
}
