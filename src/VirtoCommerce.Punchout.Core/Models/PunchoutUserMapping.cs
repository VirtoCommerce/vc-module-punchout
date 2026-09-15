using System;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.Punchout.Core.Models;

/// <summary>
/// Links the identity a procurement system sends in Header/Sender/Credential/Identity
/// to the platform user the punchout session is created for.
/// </summary>
public class PunchoutUserMapping : AuditableEntity, ICloneable
{
    public bool IsActive { get; set; }

    /// <summary>
    /// The identity of the user in the external procurement system (cXML Header/Sender/Credential/Identity).
    /// Unique across the store: it is the only thing the setup request presents to say who is punching out.
    /// </summary>
    public string ExternalId { get; set; }

    /// <summary>
    /// The platform user (security account) the session is created for.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// The user name of <see cref="UserId"/>, kept alongside it so that the admin UI does not have to
    /// resolve every account it lists.
    /// </summary>
    public string UserName { get; set; }

    /// <summary>
    /// The contact the security account belongs to. Only used to find the mapping from the member details blade.
    /// </summary>
    public string MemberId { get; set; }

    public virtual object Clone()
    {
        return MemberwiseClone();
    }
}
