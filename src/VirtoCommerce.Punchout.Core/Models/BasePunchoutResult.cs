namespace VirtoCommerce.Punchout.Core.Models;

public abstract class BasePunchoutResult
{
    public string Status { get; set; }

    public string ErrorMessage { get; set; }
}
