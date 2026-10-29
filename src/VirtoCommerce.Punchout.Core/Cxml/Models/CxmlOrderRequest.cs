using System.Collections.Generic;
using System.Xml.Serialization;

namespace VirtoCommerce.Punchout.Core.Cxml.Models;

public class CxmlOrderRequest
{
    [XmlElement("OrderRequestHeader")]
    public CxmlOrderRequestHeader OrderRequestHeader { get; set; }

    [XmlElement("ItemOut")]
    public List<CxmlItemOut> Items { get; set; }
}
