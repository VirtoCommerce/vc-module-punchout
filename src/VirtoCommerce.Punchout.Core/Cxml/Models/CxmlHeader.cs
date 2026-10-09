using System.Xml.Serialization;

namespace VirtoCommerce.Punchout.Core.Cxml.Models;

public class CxmlHeader
{
    [XmlElement("From")]
    public CxmlFrom From { get; set; }

    [XmlElement("To")]
    public CxmlTo To { get; set; }

    [XmlElement("Sender")]
    public CxmlSender Sender { get; set; }
}
