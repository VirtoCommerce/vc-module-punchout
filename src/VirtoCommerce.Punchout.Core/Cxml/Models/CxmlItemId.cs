using System.Xml.Serialization;

namespace VirtoCommerce.Punchout.Core.Cxml.Models;

public class CxmlItemId
{
    [XmlElement("SupplierPartID")]
    public string SupplierPartId { get; set; }

    [XmlElement("SupplierPartAuxiliaryID")]
    public string SupplierPartAuxiliaryId { get; set; }
}
