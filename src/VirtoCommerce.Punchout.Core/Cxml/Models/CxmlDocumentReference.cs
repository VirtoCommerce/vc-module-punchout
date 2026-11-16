using System.Xml.Serialization;

namespace VirtoCommerce.Punchout.Core.Cxml.Models;

/// <summary>
/// Reference to an earlier cXML document, e.g. the previous version of the OrderRequest in an update or delete.
/// </summary>
public class CxmlDocumentReference
{
    [XmlAttribute("payloadID")]
    public string PayloadId { get; set; }
}
