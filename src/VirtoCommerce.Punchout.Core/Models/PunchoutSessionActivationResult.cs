namespace VirtoCommerce.Punchout.Core.Models;

public class PunchoutSessionActivationResult
{
    public string Error { get; set; }

    public string PunchoutCartId { get; set; }

    public string PunchoutCartName { get; set; }

    public int? ExpiresIn { get; set; }
}
