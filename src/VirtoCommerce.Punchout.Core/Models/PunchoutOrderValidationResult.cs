using VirtoCommerce.StoreModule.Core.Model;

namespace VirtoCommerce.Punchout.Core.Models;

public class PunchoutOrderValidationResult
{
    public bool IsValid => Error is null;

    public PunchoutOrderResult Error { get; set; }

    public PunchoutConfiguration Configuration { get; set; }

    public PunchoutUserMapping UserMapping { get; set; }

    public Store Store { get; set; }

    public static PunchoutOrderValidationResult Valid(PunchoutConfiguration configuration, PunchoutUserMapping userMapping, Store store) =>
        new() { Configuration = configuration, UserMapping = userMapping, Store = store };

    public static PunchoutOrderValidationResult Invalid(string status, string errorMessage = null) =>
        new() { Error = PunchoutOrderResult.Error(status, errorMessage) };
}
