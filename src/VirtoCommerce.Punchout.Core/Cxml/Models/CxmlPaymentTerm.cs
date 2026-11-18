using System.Xml.Serialization;

namespace VirtoCommerce.Punchout.Core.Cxml.Models;

public class CxmlPaymentTerm
{
    [XmlAttribute("payInNumberOfDays")]
    public int PayInNumberOfDays { get; set; }
}
