using System.Xml.Serialization;

namespace VirtoCommerce.Punchout.Core.Cxml.Models;

public class CxmlContact
{
    /// <summary>
    /// Role of the contact, e.g. endUser, administrator or technicalSupport.
    /// </summary>
    [XmlAttribute("role")]
    public string Role { get; set; }

    [XmlElement("Name")]
    public CxmlName Name { get; set; }

    [XmlElement("Email")]
    public string Email { get; set; }
}
