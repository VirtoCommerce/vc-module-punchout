using System.Collections.Generic;
using System.Xml.Serialization;

namespace VirtoCommerce.Punchout.Core.Cxml.Models;

public class PunchoutSetupRequest
{
    [XmlAttribute("operation")]
    public string Operation { get; set; }

    [XmlElement("BuyerCookie")]
    public string BuyerCookie { get; set; }

    [XmlElement("BrowserFormPost")]
    public BrowserFormPost BrowserFormPost { get; set; }

    [XmlElement("Extrinsic")]
    public List<Extrinsic> Extrinsics { get; set; }
}
