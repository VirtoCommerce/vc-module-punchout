using System.Xml.Serialization;

namespace VirtoCommerce.Punchout.Core.Cxml.Models;

public class Credential
{
    [XmlAttribute("domain")]
    public string Domain { get; set; }

    [XmlElement("Identity")]
    public string Identity { get; set; }

    [XmlElement("SharedSecret")]
    public string SharedSecret { get; set; }
}
