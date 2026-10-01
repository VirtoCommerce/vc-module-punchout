namespace VirtoCommerce.Punchout.Core.Models;

public class PunchoutSetupResult
{
    public string Status { get; set; }

    public string StartPage { get; set; }

    public string Message { get; set; }

    public static PunchoutSetupResult Success(string startPage) => new() { Status = PunchoutSetupStatus.Success, StartPage = startPage };

    public static PunchoutSetupResult Error(string status, string message = null) => new() { Status = status, Message = message };
}
