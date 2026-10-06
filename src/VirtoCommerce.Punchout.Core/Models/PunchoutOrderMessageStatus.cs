namespace VirtoCommerce.Punchout.Core.Models;

/// <summary>
/// Reasons a punchout order message cannot be built.
/// </summary>
public static class PunchoutOrderMessageStatus
{
    public const string Error = "Error";

    public const string SessionNotFound = "SessionNotFound";

    public const string SessionExpired = "SessionExpired";

    public const string StoreNotConfigured = "StoreNotConfigured";
}
