namespace VirtoCommerce.Punchout.Core.Models;

public class PunchoutOrderMessageRequest
{
    public string SessionId { get; set; }

    public string StoreId { get; set; }

    public string CultureName { get; set; }

    public string CurrencyCode { get; set; }

    public string UserId { get; set; }

    public string OrganizationId { get; set; }
}
