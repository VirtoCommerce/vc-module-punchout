namespace VirtoCommerce.Punchout.Core.Models;

public static class PunchoutOrganizationUserStatus
{
    public const string Found = "Found";

    public const string NotFound = "NotFound";

    /// <summary>
    /// More than one user of the organization matches.
    /// </summary>
    public const string Ambiguous = "Ambiguous";
}
