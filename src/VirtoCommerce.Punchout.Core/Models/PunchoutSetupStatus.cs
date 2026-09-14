namespace VirtoCommerce.Punchout.Core.Models;

/// <summary>
/// Outcome of a punchout setup request in business terms. Translated to a cXML status by the setup mapper.
/// </summary>
public enum PunchoutSetupStatus
{
    Success,

    /// <summary>
    /// No active integration matches the sender identity, or the shared secret does not match it.
    /// </summary>
    InvalidCredentials,

    /// <summary>
    /// The request could not be understood, e.g. it is not a well-formed cXML document.
    /// </summary>
    InvalidRequest,

    /// <summary>
    /// The credentials are valid, but the integration cannot be served, e.g. its store has no storefront URL.
    /// </summary>
    StoreNotConfigured,
}
