using System.Collections.Generic;
using System.Xml.Serialization;

namespace VirtoCommerce.Punchout.Core.Cxml.Models;

public class CxmlPunchoutOrderMessage
{
    [XmlElement("BuyerCookie")]
    public string BuyerCookie { get; set; }

    [XmlElement("PunchOutOrderMessageHeader")]
    public CxmlPunchoutOrderMessageHeader PunchOutOrderMessageHeader { get; set; }

    [XmlElement("ItemIn")]
    public List<CxmlItemIn> Items { get; set; }
}
