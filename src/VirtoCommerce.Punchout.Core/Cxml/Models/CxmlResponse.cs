using System.Xml.Serialization;

namespace VirtoCommerce.Punchout.Core.Cxml.Models;

public class CxmlResponse
{
    [XmlElement("Status")]
    public CxmlStatus Status { get; set; }

    [XmlElement("PunchOutSetupResponse")]
    public CxmlPunchoutSetupResponse PunchOutSetupResponse { get; set; }
}
