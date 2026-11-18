using System.Xml.Serialization;

namespace VirtoCommerce.Punchout.Core.Cxml.Models;

public class CxmlItemIn
{
    [XmlAttribute("quantity")]
    public int Quantity { get; set; }

    [XmlElement("ItemID")]
    public CxmlItemId ItemId { get; set; }

    [XmlElement("ItemDetail")]
    public CxmlItemDetail ItemDetail { get; set; }
}
