using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Punchout.Core.Cxml;
using VirtoCommerce.Punchout.Core.Cxml.Models;
using VirtoCommerce.Punchout.Core.Models;

namespace VirtoCommerce.Punchout.Data.Cxml.Extensions;

public static class CxmlMappingExtensions
{
    public static PunchoutUserContext ToUserContext(this IList<CxmlContact> contacts)
    {
        if (contacts.IsNullOrEmpty())
        {
            return null;
        }

        // The person who started the session is the endUser. Fall back to the first contact for buyers that send one without a role.
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

    /// <summary>
    /// cXML amounts are formatted with the invariant culture. Returns null when the value is missing or malformed.
    /// </summary>
    public static decimal? ToDecimal(this CxmlMoney money)
    {
        return decimal.TryParse(money?.Value?.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var amount)
            ? amount
            : null;
    }

    /// <summary>
    /// Parses an ISO 8601 cXML timestamp. Returns null when the value is missing or malformed.
    /// </summary>
    public static DateTimeOffset? ToDateTimeOffset(this string timestamp)
    {
        return DateTimeOffset.TryParse(timestamp?.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var result)
            ? result
            : null;
    }
}
