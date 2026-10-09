using System.Xml.Serialization;

namespace VirtoCommerce.Punchout.Core.Cxml.Models;

public class CxmlMessage
{
    [XmlAttribute("deploymentMode")]
    public string DeploymentMode { get; set; }

    [XmlElement("PunchOutOrderMessage")]
    public CxmlPunchoutOrderMessage PunchOutOrderMessage { get; set; }
}
