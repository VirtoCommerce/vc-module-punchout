using VirtoCommerce.Platform.Core.Security;

namespace VirtoCommerce.Punchout.Core.Models;

/// <summary>
/// The user of an organization found by email.
/// </summary>
public class PunchoutOrganizationUserResult
{
    /// <summary>
    /// <see cref="PunchoutOrganizationUserStatus"/>
    /// </summary>
    public string Status { get; set; }

    public ApplicationUser User { get; set; }

    public int UserMatchCount { get; set; }

    public static PunchoutOrganizationUserResult Found(ApplicationUser user) =>
        new() { Status = PunchoutOrganizationUserStatus.Found, User = user, UserMatchCount = 1 };

    public static PunchoutOrganizationUserResult NotFound() =>
        new() { Status = PunchoutOrganizationUserStatus.NotFound };

    public static PunchoutOrganizationUserResult Ambiguous(int matchCount) =>
        new() { Status = PunchoutOrganizationUserStatus.Ambiguous, UserMatchCount = matchCount };
}
