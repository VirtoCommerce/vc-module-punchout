using System.Collections.Generic;
using System.Xml.Serialization;

namespace VirtoCommerce.Punchout.Core.Cxml.Models;

public class CxmlOrderRequestHeader
{
    /// <summary>
    /// Purchase order number assigned by the buying organization.
    /// </summary>
    [XmlAttribute("orderID")]
    public string OrderId { get; set; }

    [XmlAttribute("orderDate")]
    public string OrderDate { get; set; }

    /// <summary>
    /// 'new', 'update' or 'delete'.
    /// </summary>
    [XmlAttribute("type")]
    public string Type { get; set; }

    /// <summary>
    /// Version of the purchase order: 1 for the original order, incremented by each update or delete.
    /// </summary>
    [XmlAttribute("orderVersion")]
    public string OrderVersion { get; set; }

    /// <summary>
    /// The previous version of the order, required for 'update' and 'delete'.
    /// </summary>
    [XmlElement("DocumentReference")]
    public CxmlDocumentReference DocumentReference { get; set; }

    [XmlElement("Total")]
    public CxmlAmount Total { get; set; }

    [XmlElement("ShipTo")]
    public CxmlShipTo ShipTo { get; set; }

    [XmlElement("BillTo")]
    public CxmlBillTo BillTo { get; set; }

    [XmlElement("Shipping")]
    public CxmlShipping Shipping { get; set; }

    [XmlElement("PaymentTerm")]
    public CxmlPaymentTerm PaymentTerm { get; set; }

    [XmlElement("Contact")]
    public List<CxmlContact> Contacts { get; set; }

    [XmlElement("Extrinsic")]
    public List<CxmlExtrinsic> Extrinsics { get; set; }
}
