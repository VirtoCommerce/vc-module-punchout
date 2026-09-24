using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Punchout.Core.Cxml;
using VirtoCommerce.Punchout.Core.Cxml.Models;
using VirtoCommerce.Punchout.Core.Cxml.Services;
using VirtoCommerce.Punchout.Core.Models;

namespace VirtoCommerce.Punchout.Data.Cxml.Services;

public class PunchoutSetupMapper : IPunchoutSetupMapper
{
    public virtual PunchoutSetupContext MapRequest(CxmlDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var setupContext = AbstractTypeFactory<PunchoutSetupContext>.TryCreateInstance();

        var header = document.Header;
        var setupRequest = document.Request?.PunchOutSetupRequest;

        setupContext.From = header?.From?.Credential?.Identity;
        setupContext.FromDomain = header?.From?.Credential?.Domain;

        setupContext.To = header?.To?.Credential?.Identity;
        setupContext.ToDomain = header?.To?.Credential?.Domain;

        setupContext.Sender = header?.Sender?.Credential?.Identity;
        setupContext.SenderDomain = header?.Sender?.Credential?.Domain;
        setupContext.SharedSecret = header?.Sender?.Credential?.SharedSecret;

        setupContext.BuyerCookie = setupRequest?.BuyerCookie;
        setupContext.ReturnUrl = setupRequest?.BrowserFormPost?.Url;
        setupContext.Extrinsics = MapExtrinsics(setupRequest?.Extrinsics);
        setupContext.User = MapUser(setupRequest?.Contacts);

        return setupContext;
    }

    protected virtual PunchoutUserContext MapUser(IList<CxmlContact> contacts)
    {
        if (contacts.IsNullOrEmpty())
        {
            return null;
        }

        // The person who started the session is he endUser. Fall back to the first contact for buyers that send one without a role.
        var contact = contacts.FirstOrDefault(x => x.Role.EqualsIgnoreCase(CxmlConstants.ContactRole.EndUser))
            ?? contacts[0];

        var name = contact.Name?.Value?.Trim();
        var email = contact.Email?.Trim();

        if (string.IsNullOrEmpty(name) && string.IsNullOrEmpty(email))
        {
            return null;
        }

        var user = AbstractTypeFactory<PunchoutUserContext>.TryCreateInstance();

        user.Name = name;
        user.Email = email;

        return user;
    }

    public virtual CxmlDocument MapResponse(PunchoutSetupResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var document = AbstractTypeFactory<CxmlDocument>.TryCreateInstance();

        document.PayloadId = CreatePayloadId();
        document.Timestamp = DateTimeOffset.UtcNow.ToString(CxmlConstants.TimestampFormat, CultureInfo.InvariantCulture);

        var (code, text) = MapStatus(result.Status);

        document.Response = new CxmlResponse
        {
            Status = new CxmlStatus
            {
                Code = code,
                Text = text,
                Message = result.Message,
            },
        };

        if (!string.IsNullOrEmpty(result.StartPage))
        {
            document.Response.PunchOutSetupResponse = new CxmlPunchoutSetupResponse
            {
                StartPage = new CxmlStartPage { Url = result.StartPage },
            };
        }

        return document;
    }

    protected virtual (string Code, string Text) MapStatus(string status)
    {
        return status switch
        {
            PunchoutSetupStatus.Success => (CxmlConstants.Status.OkCode, CxmlConstants.Status.OkText),
            PunchoutSetupStatus.InvalidCredentials => (CxmlConstants.Status.UnauthorizedCode, CxmlConstants.Status.UnauthorizedText),
            PunchoutSetupStatus.InvalidRequest => (CxmlConstants.Status.BadRequestCode, CxmlConstants.Status.BadRequestText),
            PunchoutSetupStatus.UserNotFound => (CxmlConstants.Status.UnauthorizedCode, CxmlConstants.Status.UnauthorizedText),
            PunchoutSetupStatus.ReturnUrlNotAllowed => (CxmlConstants.Status.BadRequestCode, CxmlConstants.Status.BadRequestText),
            PunchoutSetupStatus.StoreNotConfigured => (CxmlConstants.Status.InternalServerErrorCode, CxmlConstants.Status.InternalServerErrorText),
            _ => (CxmlConstants.Status.InternalServerErrorCode, CxmlConstants.Status.InternalServerErrorText),
        };
    }

    protected virtual string CreatePayloadId()
    {
        return $"{DateTime.UtcNow.Ticks}.{Guid.NewGuid():N}@virtocommerce.com";
    }

    private static IDictionary<string, string> MapExtrinsics(IList<CxmlExtrinsic> extrinsics)
    {
        if (extrinsics.IsNullOrEmpty())
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        // Duplicate names are legal in cXML, the last one wins 
        return extrinsics
            .Where(x => !string.IsNullOrEmpty(x.Name))
            .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Last().Value, StringComparer.OrdinalIgnoreCase);
    }
}
