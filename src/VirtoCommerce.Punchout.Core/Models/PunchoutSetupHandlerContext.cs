namespace VirtoCommerce.Punchout.Core.Models;

public class PunchoutSetupHandlerContext : PunchoutHandlerContext
{
    public PunchoutSetupRequest Request { get; set; }

    public PunchoutUserMapping UserMapping { get; set; }

    /// <summary>
    /// The session is not saved yet, changes made by the handler are persisted.
    /// </summary>
    public PunchoutSession Session { get; set; }

    /// <summary>
    /// The start page URL with the session token returned to the buyer. The handler may replace it.
    /// </summary>
    public string StartPage { get; set; }
}
