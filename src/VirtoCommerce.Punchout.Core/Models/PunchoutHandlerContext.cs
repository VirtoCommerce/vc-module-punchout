namespace VirtoCommerce.Punchout.Core.Models;

public abstract class PunchoutHandlerContext
{
    public PunchoutConfiguration Configuration { get; set; }

    public string ErrorCode { get; private set; }

    public string ErrorMessage { get; private set; }

    public bool IsFailed => ErrorCode is not null;

    /// <summary>
    /// Stops the transaction, nothing is persisted and the code is returned to the buyer.
    /// </summary>
    public virtual void Fail(string code, string message = null)
    {
        ErrorCode = code;
        ErrorMessage = message;
    }
}
