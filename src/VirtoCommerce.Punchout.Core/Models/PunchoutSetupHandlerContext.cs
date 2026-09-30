namespace VirtoCommerce.Punchout.Core.Models;

public class PunchoutSetupHandlerContext : PunchoutHandlerContext
{
    public PunchoutSetupRequest Request { get; set; }

    public PunchoutUserMapping UserMapping { get; set; }

    /// <summary>
    /// The session is not saved yet, changes made by the handler are persisted.
    /// </summary>
    public PunchoutSession Session { get; set; }
}
