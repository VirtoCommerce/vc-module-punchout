using System;
using System.Collections.Generic;

namespace VirtoCommerce.Punchout.Core.Models;

public class PunchoutOrderRequestData : BasePunchoutRequest
{
    /// <summary>
    /// Purchase order number assigned by the buying organization.
    /// </summary>
    public string OrderId { get; set; }

    public DateTimeOffset? OrderDate { get; set; }

    /// <summary>
    /// 'new', 'update' or 'delete'.
    /// </summary>
    public string OrderType { get; set; }

    /// <summary>
    /// Version of the purchase order (OrderRequestHeader/@orderVersion).
    /// </summary>
    public string OrderVersion { get; set; }

    /// <summary>
    /// Payload id of the previous version of the order (OrderRequestHeader/DocumentReference/@payloadID).
    /// </summary>
    public string PreviousPayloadId { get; set; }

    /// <summary>
    /// The received cXML document with credentials removed.
    /// </summary>
    public string Cxml { get; set; }

    public string Currency { get; set; }

    public decimal? Total { get; set; }

    public decimal? ShippingAmount { get; set; }

    public string ShippingDescription { get; set; }

    public int? PaymentTermDays { get; set; }

    public PunchoutAddress ShipTo { get; set; }

    public PunchoutAddress BillTo { get; set; }

    /// <summary>
    /// Null when the request carries no usable Contact.
    /// </summary>
    public PunchoutUserContext User { get; set; }

    public IList<PunchoutOrderRequestItem> Items { get; set; } = [];
}
