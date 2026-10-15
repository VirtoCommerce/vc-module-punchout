namespace VirtoCommerce.Punchout.Core.Models;

/// <summary>
/// Processing state of a stored punchout order request.
/// </summary>
public static class PunchoutOrderRequestStatus
{
    /// <summary>
    /// The order is being created. Repeated requests are not processed.
    /// </summary>
    public const string Processing = "Processing";

    /// <summary>
    /// The order is created. Repeated requests return the stored result.
    /// </summary>
    public const string Completed = "Completed";

    /// <summary>
    /// The order creation failed. Repeated requests are processed again.
    /// </summary>
    public const string Failed = "Failed";

    /// <summary>
    /// The request did not pass the validation. Repeated requests are processed again.
    /// </summary>
    public const string Rejected = "Rejected";
}
