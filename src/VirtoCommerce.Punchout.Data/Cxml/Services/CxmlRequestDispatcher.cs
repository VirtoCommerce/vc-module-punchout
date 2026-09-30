using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using VirtoCommerce.Punchout.Core.Cxml;
using VirtoCommerce.Punchout.Core.Cxml.Models;
using VirtoCommerce.Punchout.Core.Cxml.Services;

namespace VirtoCommerce.Punchout.Data.Cxml.Services;

public class CxmlRequestDispatcher(
    IEnumerable<ICxmlRequestHandler> handlers,
    ICxmlResponseFactory responseFactory,
    ILogger<CxmlRequestDispatcher> logger)
    : ICxmlRequestDispatcher
{
    public virtual async Task<CxmlDocument> DispatchAsync(CxmlDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (document.Request is null)
        {
            logger.LogWarning("cXML document '{PayloadId}' has no Request element.", document.PayloadId);

            return responseFactory.CreateResponse(CxmlConstants.Status.BadRequestCode, CxmlConstants.Status.BadRequestText,
                "The cXML document has no Request element.");
        }

        var handler = FindHandler(document);

        if (handler is null)
        {
            logger.LogWarning("cXML document '{PayloadId}' has a request type that is not supported.", document.PayloadId);

            return responseFactory.CreateResponse(CxmlConstants.Status.NotImplementedCode, CxmlConstants.Status.NotImplementedText,
                "The request type is not supported.");
        }

        return await handler.HandleAsync(document);
    }

    protected virtual ICxmlRequestHandler FindHandler(CxmlDocument document)
    {
        // The last registered handler wins
        return handlers.LastOrDefault(x => x.CanHandle(document));
    }
}
