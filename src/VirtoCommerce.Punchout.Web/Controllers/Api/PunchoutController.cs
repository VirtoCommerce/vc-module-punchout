using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using VirtoCommerce.Punchout.Core.Cxml;
using VirtoCommerce.Punchout.Core.Cxml.Models;
using VirtoCommerce.Punchout.Core.Cxml.Services;

namespace VirtoCommerce.Punchout.Web.Controllers.Api;

[Authorize]
[Route("api/punchout/cxml")]
public class PunchoutController : Controller
{
    private readonly ICxmlSerializer _serializer;
    private readonly ICxmlRequestDispatcher _dispatcher;
    private readonly ICxmlResponseFactory _responseFactory;
    private readonly ILogger<PunchoutController> _logger;

    public PunchoutController(
        ICxmlSerializer serializer,
        ICxmlRequestDispatcher dispatcher,
        ICxmlResponseFactory responseFactory,
        ILogger<PunchoutController> logger)
    {
        _serializer = serializer;
        _dispatcher = dispatcher;
        _responseFactory = responseFactory;
        _logger = logger;
    }

    // POST: api/punchout/cxml
    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> Process()
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
            _logger.LogWarning(ex, "Failed to deserialize a cXML request.");

            return CxmlResponse(_responseFactory.CreateResponse(CxmlConstants.Status.BadRequestCode, CxmlConstants.Status.BadRequestText,
                "The request body is not a well-formed cXML document."));
        }

        CxmlDocument response;

        try
        {
            response = await _dispatcher.DispatchAsync(request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process cXML request '{PayloadId}'.", request.PayloadId);

            response = _responseFactory.CreateResponse(CxmlConstants.Status.InternalServerErrorCode, CxmlConstants.Status.InternalServerErrorText);
        }

        return CxmlResponse(response);
    }

    /// <summary>
    /// cXML carries the outcome in the Status element of a 200 OK response,
    /// the caller always gets a cXML document rather than an HTTP error page.
    /// </summary>
    private ContentResult CxmlResponse(CxmlDocument response)
    {
        var responseXml = _serializer.Serialize(response);

        return Content(responseXml, "text/xml", Encoding.UTF8);
    }
}
