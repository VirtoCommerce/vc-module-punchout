using System.Xml.Serialization;

namespace VirtoCommerce.Punchout.Core.Cxml.Models;

public class CxmlTo
{
    [XmlElement("Credential")]
    public CxmlCredential Credential { get; set; }
}
