using VirtoCommerce.StoreModule.Core.Model;

namespace VirtoCommerce.Punchout.Core.Models;

public class PunchoutSetupValidationResult
{
    public bool IsValid => Error is null;

    public PunchoutSetupResult Error { get; set; }

    public PunchoutConfiguration Configuration { get; set; }

    public PunchoutUserMapping UserMapping { get; set; }

    /// <summary>
    /// The mapped user, or the user resolved in the mapped organization.
    /// </summary>
    public string UserId { get; set; }

    public string OrganizationId { get; set; }

    public Store Store { get; set; }

    public string StorefrontUrl { get; set; }

    public static PunchoutSetupValidationResult Valid(
        PunchoutConfiguration configuration,
        PunchoutUserMapping userMapping,
        string userId,
        string organizationId,
        Store store,
        string storefrontUrl) =>
        new()
        {
            Configuration = configuration,
            UserMapping = userMapping,
            UserId = userId,
            OrganizationId = organizationId,
            Store = store,
            StorefrontUrl = storefrontUrl,
        };

    public static PunchoutSetupValidationResult Invalid(string status, string errorMessage = null) =>
        new() { Error = PunchoutSetupResult.Error(status, errorMessage) };
}
