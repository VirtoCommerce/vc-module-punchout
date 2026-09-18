using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using VirtoCommerce.Punchout.Core.Cxml.Models;
using VirtoCommerce.Punchout.Core.Cxml.Services;
using VirtoCommerce.Punchout.Core.Models;
using VirtoCommerce.Punchout.Core.Services;

namespace VirtoCommerce.Punchout.Web.Controllers.Api;

[Authorize]
[Route("api/punchout/cxml")]
public class PunchoutController : Controller
{
    private readonly ICxmlSerializer _serializer;
    private readonly IPunchoutSetupMapper _mapper;
    private readonly IPunchoutSetupService _setupService;
    private readonly ILogger<PunchoutController> _logger;

    public PunchoutController(
        ICxmlSerializer serializer,
        IPunchoutSetupMapper mapper,
        IPunchoutSetupService punchoutSetupService,
        ILogger<PunchoutController> logger)
    {
        _serializer = serializer;
        _mapper = mapper;
        _setupService = punchoutSetupService;
        _logger = logger;
    }

    // POST: api/punchout/cxml/setup
    [HttpPost("setup")]
    [AllowAnonymous]
    public async Task<IActionResult> Setup()
    {
        using var reader = new StreamReader(Request.Body);

        var xml = await reader.ReadToEndAsync();

        CxmlDocument request;

        try
        {
            request = _serializer.Deserialize<CxmlDocument>(xml);
        }
        catch (Exception ex) when (ex is XmlException or InvalidOperationException)
        {
            _logger.LogWarning(ex, "Failed to deserialize a punchout setup request.");

            return CxmlResponse(PunchoutSetupResult.Error(PunchoutSetupStatus.InvalidRequest,
                "The request body is not a well-formed cXML document."));
        }

        var requestContext = _mapper.MapRequest(request);

        var punchoutResult = await _setupService.ProcessAsync(requestContext);

        return CxmlResponse(punchoutResult);
    }

    /// <summary>
    /// cXML carries the outcome in the Status element of a 200 OK response, so that the caller always
    /// gets a cXML document it can parse rather than an HTTP error page.
    /// </summary>
    private ContentResult CxmlResponse(PunchoutSetupResult result)
    {
        var response = _mapper.MapResponse(result);

        var responseXml = _serializer.Serialize(response);

        return Content(responseXml, "text/xml", Encoding.UTF8);
    }
}
