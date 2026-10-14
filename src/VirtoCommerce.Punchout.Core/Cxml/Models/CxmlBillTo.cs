using System.Xml.Serialization;

namespace VirtoCommerce.Punchout.Core.Cxml.Models;

public class CxmlBillTo
{
    [XmlElement("Address")]
    public CxmlAddress Address { get; set; }
}
