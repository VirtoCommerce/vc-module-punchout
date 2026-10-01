using System;
using System.Threading.Tasks;
using VirtoCommerce.Punchout.Core.Cxml.Models;
using VirtoCommerce.Punchout.Core.Cxml.Services;
using VirtoCommerce.Punchout.Core.Services;

namespace VirtoCommerce.Punchout.Data.Cxml.Services;

public class CxmlPunchoutSetupRequestHandler(
    IPunchoutSetupMapper mapper,
    IPunchoutSetupService setupService)
    : ICxmlRequestHandler
{
    public virtual bool CanHandle(CxmlDocument document)
    {
        return document?.Request?.PunchOutSetupRequest is not null;
    }

    public virtual async Task<CxmlDocument> HandleAsync(CxmlDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var request = mapper.MapRequest(document);

        var result = await setupService.ProcessAsync(request);

        return mapper.MapResponse(result);
    }
}
