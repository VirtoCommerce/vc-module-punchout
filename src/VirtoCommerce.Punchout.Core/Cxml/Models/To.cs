using System.Xml.Serialization;

namespace VirtoCommerce.Punchout.Core.Cxml.Models;

public class To
{
    [XmlElement("Credential")]
    public Credential Credential { get; set; }
}
