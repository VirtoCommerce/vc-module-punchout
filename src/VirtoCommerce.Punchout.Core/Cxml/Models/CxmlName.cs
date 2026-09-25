using System.Xml.Serialization;

namespace VirtoCommerce.Punchout.Core.Cxml.Models;

public class CxmlName
{
    [XmlAttribute("lang", Namespace = "http://www.w3.org/XML/1998/namespace")]
    public string Language { get; set; }

    [XmlText]
    public string Value { get; set; }
}
