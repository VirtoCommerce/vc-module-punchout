namespace VirtoCommerce.Punchout.Core.Models;

public abstract class PunchoutHandlerContext
{
    public PunchoutConfiguration Configuration { get; set; }

    public string ErrorStatus { get; private set; }

    public string ErrorMessage { get; private set; }

    public bool IsFailed => ErrorStatus is not null;

    /// <summary>
    /// Stops the transaction, nothing is persisted and the status is returned to the buyer.
    /// </summary>
    public virtual void Fail(string status, string message = null)
    {
        ErrorStatus = status;
        ErrorMessage = message;
    }
}
