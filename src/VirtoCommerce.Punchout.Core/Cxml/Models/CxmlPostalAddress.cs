using System.Collections.Generic;
using System.Xml.Serialization;

namespace VirtoCommerce.Punchout.Core.Cxml.Models;

public class CxmlPostalAddress
{
    [XmlAttribute("name")]
    public string Name { get; set; }

    /// <summary>
    /// cXML allows several DeliverTo lines.
    /// </summary>
    [XmlElement("DeliverTo")]
    public List<string> DeliverTo { get; set; }

    /// <summary>
    /// cXML allows several Street lines.
    /// </summary>
    [XmlElement("Street")]
    public List<string> Streets { get; set; }

    [XmlElement("City")]
    public string City { get; set; }

    [XmlElement("State")]
    public string State { get; set; }

    [XmlElement("PostalCode")]
    public string PostalCode { get; set; }

    [XmlElement("Country")]
    public CxmlCountry Country { get; set; }
}
