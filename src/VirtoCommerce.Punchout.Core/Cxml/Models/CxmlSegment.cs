using System.Xml.Serialization;

namespace VirtoCommerce.Punchout.Core.Cxml.Models;

public class CxmlSegment
{
    /// <summary>
    /// Accounting segment type, e.g. 'CostCenter', 'Account'.
    /// </summary>
    [XmlAttribute("type")]
    public string Type { get; set; }

    [XmlAttribute("id")]
    public string Id { get; set; }

    [XmlAttribute("description")]
    public string Description { get; set; }
}
