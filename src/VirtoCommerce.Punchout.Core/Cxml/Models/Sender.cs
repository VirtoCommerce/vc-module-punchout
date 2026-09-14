using System.Xml.Serialization;

namespace VirtoCommerce.Punchout.Core.Cxml.Models;

public class Sender
{
    [XmlElement("Credential")]
    public Credential Credential { get; set; }

    [XmlElement("UserAgent")]
    public string UserAgent { get; set; }
}
