namespace VirtoCommerce.Punchout.Core.Models;

public class PunchoutCheckoutResult
{
    public string Url { get; set; }
    public string FormField { get; set; }
    public string Cxml { get; set; }
    public string ErrorCode { get; set; }
}
