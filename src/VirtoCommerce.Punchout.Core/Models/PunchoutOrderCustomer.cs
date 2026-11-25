namespace VirtoCommerce.Punchout.Core.Models;

/// <summary>
/// The customer a punchout order is placed for: the mapped user, or the end user resolved in the mapped organization.
/// </summary>
public class PunchoutOrderCustomer
{
    public string UserId { get; set; }

    public string UserName { get; set; }

    /// <summary>
    /// The contact the security account belongs to.
    /// </summary>
    public string MemberId { get; set; }

    /// <summary>
    /// The mapped organization. Empty for a user mapping, the order then takes the organization of the contact.
    /// </summary>
    public string OrganizationId { get; set; }
}
