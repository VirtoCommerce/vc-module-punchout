using System.Xml.Serialization;

namespace VirtoCommerce.Punchout.Core.Cxml.Models;

public class Response
{
    [XmlElement("PunchoutSetupResponse")]
    public PunchoutSetupResponse PunchoutSetupResponse { get; set; }
}
