using System.Xml.Serialization;

namespace VirtoCommerce.Punchout.Core.Cxml.Models;

public class CxmlShipping
{
    [XmlElement("Money")]
    public CxmlMoney Money { get; set; }

    [XmlElement("Description")]
    public CxmlDescription Description { get; set; }
}
