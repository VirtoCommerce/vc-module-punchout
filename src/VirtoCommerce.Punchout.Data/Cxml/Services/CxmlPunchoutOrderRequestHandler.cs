using System;
using System.Threading.Tasks;
using VirtoCommerce.Punchout.Core.Cxml.Models;
using VirtoCommerce.Punchout.Core.Cxml.Services;
using VirtoCommerce.Punchout.Core.Services;

namespace VirtoCommerce.Punchout.Data.Cxml.Services;

public class CxmlPunchoutOrderRequestHandler(
        IPunchoutOrderMapper mapper,
        IPunchoutOrderRequestProcessor orderProcessor)
    : ICxmlRequestHandler
{
    public bool CanHandle(CxmlDocument document)
    {
        return document?.Request?.PunchOutOrderRequest is not null;
    }

    public async Task<CxmlDocument> HandleAsync(CxmlDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var request = mapper.MapRequest(document);

        var result = await orderProcessor.ProcessAsync(request);

        return mapper.MapResponse(result);
    }
}
