using System.Collections.Generic;
using System.Xml.Serialization;

namespace VirtoCommerce.Punchout.Core.Cxml.Models;

public class CxmlItemOut
{
    /// <summary>
    /// Can be fractional for weight-based units of measure.
    /// </summary>
    [XmlAttribute("quantity")]
    public decimal Quantity { get; set; }

    [XmlAttribute("lineNumber")]
    public int LineNumber { get; set; }

    [XmlElement("ItemID")]
    public CxmlItemId ItemId { get; set; }

    [XmlElement("ItemDetail")]
    public CxmlItemDetail ItemDetail { get; set; }

    [XmlElement("Distribution")]
    public List<CxmlDistribution> Distributions { get; set; }
}
