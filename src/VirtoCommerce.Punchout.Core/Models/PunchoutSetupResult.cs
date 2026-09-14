namespace VirtoCommerce.Punchout.Core.Models;

public class PunchoutSetupResult
{
    public PunchoutSetupStatus Status { get; set; }

    /// <summary>
    /// The URL the buyer's browser is sent to. Only filled in when Status is Success/>.
    /// </summary>
    public string StartPage { get; set; }

    public string Message { get; set; }

    public static PunchoutSetupResult Success(string startPage) => new() { Status = PunchoutSetupStatus.Success, StartPage = startPage };

    public static PunchoutSetupResult Error(PunchoutSetupStatus status, string message = null) => new() { Status = status, Message = message };
}
