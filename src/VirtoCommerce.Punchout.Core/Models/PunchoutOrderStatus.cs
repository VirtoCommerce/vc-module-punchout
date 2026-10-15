namespace VirtoCommerce.Punchout.Core.Models;

/// <summary>
/// Outcome of a punchout order request. Translated to a cXML status by the order mapper.
/// </summary>
public static class PunchoutOrderStatus
{
    public const string Success = "Success";

    /// <summary>
    /// No configuration matches the shared secret, or the sender domain does not match it.
    /// </summary>
    public const string InvalidCredentials = "InvalidCredentials";

    /// <summary>
    /// The order is incomplete or malformed, e.g. has no order id or items.
    /// </summary>
    public const string InvalidRequest = "InvalidRequest";

    /// <summary>
    /// The order type is not supported, only new orders are accepted.
    /// </summary>
    public const string UnsupportedOrderType = "UnsupportedOrderType";

    /// <summary>
    /// The order currency is not available in the store.
    /// </summary>
    public const string CurrencyNotSupported = "CurrencyNotSupported";

    /// <summary>
    /// The credentials are valid, but the sender identity is not linked to any platform user.
    /// </summary>
    public const string UserNotFound = "UserNotFound";

    /// <summary>
    /// The credentials are valid, but the store does not exist or punchout is disabled for it.
    /// </summary>
    public const string StoreNotConfigured = "StoreNotConfigured";

    /// <summary>
    /// The same order is being processed by another request. The buyer may retry later.
    /// </summary>
    public const string OrderInProgress = "OrderInProgress";

    public const string OrderError = "OrderError";
}
