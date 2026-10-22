using System.Xml.Serialization;

namespace VirtoCommerce.Punchout.Core.Cxml.Models;

/// <summary>
/// Wrapper of a single Money element, used by Total, UnitPrice etc.
/// </summary>
public class CxmlAmount
{
    [XmlElement("Money")]
    public CxmlMoney Money { get; set; }
}
