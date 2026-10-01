using System.Xml.Serialization;

namespace VirtoCommerce.Punchout.Core.Cxml.Models;

public class CxmlStartPage
{
    [XmlElement("URL")]
    public string Url { get; set; }
}
