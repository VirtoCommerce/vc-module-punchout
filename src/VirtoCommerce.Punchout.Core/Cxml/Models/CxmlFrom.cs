using System.Xml.Serialization;

namespace VirtoCommerce.Punchout.Core.Cxml.Models;

public class CxmlFrom
{
    [XmlElement("Credential")]
    public CxmlCredential Credential { get; set; }
}
