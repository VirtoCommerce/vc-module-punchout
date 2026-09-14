using System.Xml.Serialization;

namespace VirtoCommerce.Punchout.Core.Cxml.Models;

public class Header
{
    [XmlElement("From")]
    public From From { get; set; }

    [XmlElement("To")]
    public To To { get; set; }

    [XmlElement("Sender")]
    public Sender Sender { get; set; }
}
