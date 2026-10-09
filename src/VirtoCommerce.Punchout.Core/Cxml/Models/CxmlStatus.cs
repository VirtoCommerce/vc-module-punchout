using System.Xml.Serialization;

namespace VirtoCommerce.Punchout.Core.Cxml.Models;

public class CxmlStatus
{
    [XmlAttribute("code")]
    public string Code { get; set; }

    [XmlAttribute("text")]
    public string Text { get; set; }

    [XmlText]
    public string Message { get; set; }
}
