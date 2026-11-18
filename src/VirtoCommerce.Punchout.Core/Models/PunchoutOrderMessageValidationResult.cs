using VirtoCommerce.StoreModule.Core.Model;

namespace VirtoCommerce.Punchout.Core.Models;

public class PunchoutOrderMessageValidationResult
{
    public bool IsValid => ErrorCode is null;

    public string ErrorCode { get; set; }

    public PunchoutSession Session { get; set; }

    public PunchoutConfiguration Configuration { get; set; }

    public Store Store { get; set; }

    public static PunchoutOrderMessageValidationResult Valid(PunchoutSession session, PunchoutConfiguration configuration, Store store) =>
        new() { Session = session, Configuration = configuration, Store = store };

    public static PunchoutOrderMessageValidationResult Invalid(string errorCode) =>
        new() { ErrorCode = errorCode };
}
