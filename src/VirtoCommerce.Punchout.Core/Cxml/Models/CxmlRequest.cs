using System.Xml.Serialization;

namespace VirtoCommerce.Punchout.Core.Cxml.Models;

public class CxmlRequest
{
    [XmlAttribute("deploymentMode")]
    public string DeploymentMode { get; set; }

    [XmlElement("PunchOutSetupRequest")]
    public CxmlPunchoutSetupRequest PunchOutSetupRequest { get; set; }
}
