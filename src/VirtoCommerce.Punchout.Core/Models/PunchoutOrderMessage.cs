using System;
using System.Collections.Generic;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.Punchout.Core.Models;

public class PunchoutOrderMessage : AuditableEntity, ICloneable
{
    public string SessionId { get; set; }

    public string Cxml { get; set; }

    /// <summary>
    /// The snapshot of the cart line items sent in the message, the customer order is created from it.
    /// </summary>
    public IList<PunchoutRequisitionItem> Items { get; set; } = [];

    public virtual object Clone()
    {
        return MemberwiseClone();
    }
}
