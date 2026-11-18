using System.Xml.Serialization;

namespace VirtoCommerce.Punchout.Core.Cxml.Models;

public class CxmlAddress
{
    [XmlAttribute("isoCountryCode")]
    public string IsoCountryCode { get; set; }

    /// <summary>
    /// Buyer-side identifier of the address.
    /// </summary>
    [XmlAttribute("addressID")]
    public string AddressId { get; set; }

    [XmlElement("Name")]
    public CxmlName Name { get; set; }

    [XmlElement("PostalAddress")]
    public CxmlPostalAddress PostalAddress { get; set; }

    [XmlElement("Email")]
    public string Email { get; set; }
}
