using System;
using System.Globalization;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Punchout.Core.Cxml;
using VirtoCommerce.Punchout.Core.Cxml.Models;
using VirtoCommerce.Punchout.Core.Cxml.Services;

namespace VirtoCommerce.Punchout.Data.Cxml.Services;

public class CxmlResponseFactory : ICxmlResponseFactory
{
    public virtual CxmlDocument CreateDocument()
    {
        var document = AbstractTypeFactory<CxmlDocument>.TryCreateInstance();

        document.PayloadId = CreatePayloadId();
        document.Timestamp = DateTimeOffset.UtcNow.ToString(CxmlConstants.TimestampFormat, CultureInfo.InvariantCulture);

        return document;
    }

    public virtual CxmlDocument CreateResponse(string code, string text, string message = null)
    {
        var document = CreateDocument();

        document.Response = new CxmlResponse
        {
            Status = new CxmlStatus
            {
                Code = code,
                Text = text,
                Message = message,
            },
        };

        return document;
    }

    protected virtual string CreatePayloadId()
    {
        return $"{DateTime.UtcNow.Ticks}.{Guid.NewGuid():N}@virtocommerce.com";
    }
}
