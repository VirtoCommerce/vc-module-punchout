using System.Collections.Generic;
using System.Xml.Serialization;

namespace VirtoCommerce.Punchout.Core.Cxml.Models;

public class CxmlPunchoutSetupRequest
{
    [XmlAttribute("operation")]
    public string Operation { get; set; }

    [XmlElement("BuyerCookie")]
    public string BuyerCookie { get; set; }

    [XmlElement("BrowserFormPost")]
    public CxmlBrowserFormPost BrowserFormPost { get; set; }

    [XmlElement("Extrinsic")]
    public List<CxmlExtrinsic> Extrinsics { get; set; }
}
