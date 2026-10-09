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

    /// <summary>
    /// Contacts of the buying side. cXML allows several, distinguished by their role.
    /// </summary>
    [XmlElement("Contact")]
    public List<CxmlContact> Contacts { get; set; }
}
