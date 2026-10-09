using System;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.Punchout.Core.Models;

/// <summary>
/// Links the identity a procurement system sends in Header/Sender/Credential/Identity to the platform user the punchout session is created for.
/// </summary>
public class PunchoutUserMapping : AuditableEntity, ICloneable
{
    public bool IsActive { get; set; }

    /// <summary>
    /// The identity of the user in the external procurement system (cXML Header/Sender/Credential/Identity).
    /// </summary>
    public string ExternalId { get; set; }

    /// <summary>
    /// The platform user the session is created for.
    /// </summary>
    public string UserId { get; set; }

    public string UserName { get; set; }

    /// <summary>
    /// The contact the security account belongs to
    /// </summary>
    public string MemberId { get; set; }

    public virtual object Clone()
    {
        return MemberwiseClone();
    }
}
