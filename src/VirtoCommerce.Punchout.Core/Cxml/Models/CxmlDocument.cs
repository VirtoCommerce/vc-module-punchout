using System.Xml.Serialization;

namespace VirtoCommerce.Punchout.Core.Cxml.Models;

[XmlRoot("cXML")]
public class CxmlDocument
{
    [XmlAttribute("payloadID")]
    public string PayloadId { get; set; }

    [XmlAttribute("timestamp")]
    public string Timestamp { get; set; }

    [XmlElement("Header")]
    public Header Header { get; set; }

    [XmlElement("Request")]
    public Request Request { get; set; }
}
