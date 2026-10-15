using System;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.Punchout.Core.Models;

public class PunchoutOrderMessage : AuditableEntity, ICloneable
{
    public string SessionId { get; set; }

    public string Cxml { get; set; }

    public virtual object Clone()
    {
        return MemberwiseClone();
    }
}
