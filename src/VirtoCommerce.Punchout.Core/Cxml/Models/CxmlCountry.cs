using System.Xml.Serialization;

namespace VirtoCommerce.Punchout.Core.Cxml.Models;

public class CxmlCountry
{
    [XmlAttribute("isoCountryCode")]
    public string IsoCountryCode { get; set; }

    [XmlText]
    public string Value { get; set; }
}
