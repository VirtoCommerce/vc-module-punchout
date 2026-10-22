using System.Xml.Serialization;

namespace VirtoCommerce.Punchout.Core.Cxml.Models;

public class CxmlClassification
{
    /// <summary>
    /// Classification system, e.g. 'UNSPSC'.
    /// </summary>
    [XmlAttribute("domain")]
    public string Domain { get; set; }

    [XmlText]
    public string Value { get; set; }
}
