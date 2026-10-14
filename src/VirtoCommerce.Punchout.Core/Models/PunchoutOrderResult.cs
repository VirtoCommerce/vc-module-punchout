namespace VirtoCommerce.Punchout.Core.Models;

public class PunchoutOrderResult : BasePunchoutResult
{
    public static PunchoutOrderResult Success() => new() { Status = PunchoutSetupStatus.Success };

    public static PunchoutOrderResult Error(string status, string errorMessage = null) => new() { Status = status, ErrorMessage = errorMessage };
}
