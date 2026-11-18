using System.Collections.Generic;
using System.Xml.Serialization;

namespace VirtoCommerce.Punchout.Core.Cxml.Models;

public class CxmlAccounting
{
    [XmlAttribute("name")]
    public string Name { get; set; }

    [XmlElement("Segment")]
    public List<CxmlSegment> Segments { get; set; }
}
