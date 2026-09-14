using System.Xml.Serialization;

namespace VirtoCommerce.Punchout.Core.Cxml.Models;

public class Request
{
    [XmlElement("PunchOutSetupRequest")]
    public PunchoutSetupRequest PunchOutSetupRequest { get; set; }
}
