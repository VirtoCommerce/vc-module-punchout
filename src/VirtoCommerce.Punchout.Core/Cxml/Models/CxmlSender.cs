using System.Xml.Serialization;

namespace VirtoCommerce.Punchout.Core.Cxml.Models;

public class CxmlSender
{
    [XmlElement("Credential")]
    public CxmlCredential Credential { get; set; }

    [XmlElement("UserAgent")]
    public string UserAgent { get; set; }
}
