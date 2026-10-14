using System.Xml.Serialization;

namespace VirtoCommerce.Punchout.Core.Cxml.Models;

public class CxmlDistribution
{
    [XmlElement("Accounting")]
    public CxmlAccounting Accounting { get; set; }

    [XmlElement("Charge")]
    public CxmlAmount Charge { get; set; }
}
