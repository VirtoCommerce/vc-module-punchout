using System;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Punchout.Core.Cxml;
using VirtoCommerce.Punchout.Core.Cxml.Models;
using VirtoCommerce.Punchout.Core.Cxml.Services;
using VirtoCommerce.Punchout.Core.Models;

namespace VirtoCommerce.Punchout.Data.Cxml.Services;

public class PunchoutOrderMapper(ICxmlResponseFactory responseFactory) : IPunchoutOrderMapper
{
    public PunchoutOrderRequest MapRequest(CxmlDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var orderContext = AbstractTypeFactory<PunchoutOrderRequest>.TryCreateInstance();

        return orderContext;
    }

    public CxmlDocument MapResponse(PunchoutOrderResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        // todo: map response codes when finalized
        var document = responseFactory.CreateResponse(CxmlConstants.Status.OkCode, CxmlConstants.Status.OkText, result.ErrorMessage);

        return document;
    }
}
