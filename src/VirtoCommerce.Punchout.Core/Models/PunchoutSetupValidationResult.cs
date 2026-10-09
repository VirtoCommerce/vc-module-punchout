namespace VirtoCommerce.Punchout.Core.Models;

public class PunchoutSetupValidationResult
{
    public bool IsValid => Error is null;

    public PunchoutSetupResult Error { get; set; }

    public PunchoutConfiguration Configuration { get; set; }

    public PunchoutUserMapping UserMapping { get; set; }

    public string StorefrontUrl { get; set; }

    public static PunchoutSetupValidationResult Valid(PunchoutConfiguration configuration, PunchoutUserMapping userMapping, string storefrontUrl) =>
        new() { Configuration = configuration, UserMapping = userMapping, StorefrontUrl = storefrontUrl };

    public static PunchoutSetupValidationResult Invalid(string status, string errorMessage = null) =>
        new() { Error = PunchoutSetupResult.Error(status, errorMessage) };
}
