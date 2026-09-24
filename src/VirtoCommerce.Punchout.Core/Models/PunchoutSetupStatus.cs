namespace VirtoCommerce.Punchout.Core.Models;

/// <summary>
/// Outcome of a punchout setup request. Translated to a cXML status by the setup mapper.
/// </summary>
public static class PunchoutSetupStatus
{
    public const string Success = "Success";

    /// <summary>
    /// No active integration matches the sender identity, or the shared secret does not match it.
    /// </summary>
    public const string InvalidCredentials = "InvalidCredentials";

    /// <summary>
    /// The request is not a well-formed cXML document.
    /// </summary>
    public const string InvalidRequest = "InvalidRequest";

    /// <summary>
    /// The credentials are valid, but the integration cannot be completed becase the store has no storefront URL.
    /// </summary>
    public const string StoreNotConfigured = "StoreNotConfigured";

    /// <summary>
    /// The credentials are valid, but the sender identity is not linked to any platform user.
    /// </summary>
    public const string UserNotFound = "UserNotFound";

    /// <summary>
    /// The URL the request asks to return to is not in the allow list of the configuration.
    /// </summary>
    public const string ReturnUrlNotAllowed = "ReturnUrlNotAllowed";
}
