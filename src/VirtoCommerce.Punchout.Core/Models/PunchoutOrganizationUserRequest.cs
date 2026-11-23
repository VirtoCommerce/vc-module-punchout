namespace VirtoCommerce.Punchout.Core.Models;

/// <summary>
/// The user of an organization to find for a punchout request.
/// </summary>
public class PunchoutOrganizationUserRequest
{
    public string OrganizationId { get; set; }

    public string Email { get; set; }

    public string StoreId { get; set; }
}
