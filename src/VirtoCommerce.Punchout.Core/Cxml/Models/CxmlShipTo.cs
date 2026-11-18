using System.Xml.Serialization;

namespace VirtoCommerce.Punchout.Core.Cxml.Models;

public class CxmlShipTo
{
    [XmlElement("Address")]
    public CxmlAddress Address { get; set; }
}
