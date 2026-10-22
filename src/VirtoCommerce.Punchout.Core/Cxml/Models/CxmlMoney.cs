using System.Xml.Serialization;

namespace VirtoCommerce.Punchout.Core.Cxml.Models;

public class CxmlMoney
{
    [XmlAttribute("currency")]
    public string Currency { get; set; }

    /// <summary>
    /// Amount formatted with the invariant culture, e.g. '125.00'.
    /// </summary>
    [XmlText]
    public string Value { get; set; }
}
