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

    /// <summary>
    /// The request is authenticated, but rejected. The configuration is known, so the request can be stored.
    /// </summary>
    public static PunchoutOrderValidationResult Invalid(string status, string errorMessage, PunchoutConfiguration configuration, PunchoutUserMapping userMapping = null, Store store = null) =>
        new() { Error = PunchoutOrderResult.Error(status, errorMessage), Configuration = configuration, UserMapping = userMapping, Store = store };
}
