using System;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.Punchout.Core.Models;

/// <summary>
/// SupplierPartAuxiliaryID of the items in PunchOutOrderMessage in the format '{LineItemId};{SessionId}'.
/// The buyer should return it unchanged in the OrderRequest.
/// </summary>
public static class PunchoutItemReference
{
    public const char Separator = ';';

    public static string Format(string lineItemId, string sessionId)
    {
        return string.IsNullOrEmpty(sessionId)
            ? lineItemId
            : $"{lineItemId}{Separator}{sessionId}";
    }

    public static (string LineItemId, string SessionId) Parse(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return (null, null);
        }

        var parts = value.Split(Separator, 2, StringSplitOptions.TrimEntries);

        return (parts[0].EmptyToNull(), parts.Length > 1 ? parts[1].EmptyToNull() : null);
    }
}
