namespace VirtoCommerce.Punchout.Core.Models;

public class PunchoutAddress
{
    /// <summary>
    /// Buyer-side identifier of the address.
    /// </summary>
    public string AddressId { get; set; }

    public string Name { get; set; }

    public string DeliverTo { get; set; }

    public string Line1 { get; set; }

    public string Line2 { get; set; }

    public string City { get; set; }

    public string RegionName { get; set; }

    public string PostalCode { get; set; }

    /// <summary>
    /// ISO 3166-1 alpha-2 country code.
    /// </summary>
    public string CountryCode { get; set; }

    public string CountryName { get; set; }

    public string Email { get; set; }
}
