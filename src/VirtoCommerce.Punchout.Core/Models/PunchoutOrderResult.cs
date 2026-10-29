namespace VirtoCommerce.Punchout.Core.Models;

public class PunchoutOrderResult : BasePunchoutResult
{
    public string CustomerOrderId { get; set; }

    public string PurchaseOrderNumber { get; set; }

    public static PunchoutOrderResult Success(string customerOrderId, string purchaseOrderNumber) =>
        new() { Status = PunchoutOrderStatus.Success, CustomerOrderId = customerOrderId, PurchaseOrderNumber = purchaseOrderNumber };

    public static PunchoutOrderResult Error(string status, string errorMessage = null) => new() { Status = status, ErrorMessage = errorMessage };
}
