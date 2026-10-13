namespace VirtoCommerce.Punchout.Core.Models;

public abstract class BasePunchoutResult
{
    public string Status { get; set; }

    public string ErrorMessage { get; set; }

    public static PunchoutSetupResult Success(string startPage) => new() { Status = PunchoutSetupStatus.Success, StartPage = startPage };

    public static PunchoutSetupResult Error(string status, string errorMessage = null) => new() { Status = status, ErrorMessage = errorMessage };
}
