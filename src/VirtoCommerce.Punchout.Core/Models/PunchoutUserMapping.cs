using System;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.Punchout.Core.Models;

/// <summary>
/// Links the identity a procurement system sends (Header/Sender/Credential/Identity by default) either to the platform user
/// the punchout session is created for, or to the organization whose users shop in punchout sessions.
/// A mapping has either <see cref="UserId"/> or <see cref="OrganizationId"/>.
/// </summary>
public class PunchoutUserMapping : AuditableEntity, ICloneable
{
    public bool IsActive { get; set; }

    /// <summary>
    /// The identity in the external procurement system (cXML Header/Sender/Credential/Identity by default).
    /// </summary>
    public string ExternalId { get; set; }

    /// <summary>
    /// The platform user the session is created for. Empty for an organization mapping.
    /// </summary>
    public string UserId { get; set; }

    public string UserName { get; set; }

    /// <summary>
    /// The contact the security account belongs to
    /// </summary>
    public string MemberId { get; set; }

    /// <summary>
    /// The organization whose users are resolved for punchout requests. Empty for a user mapping.
    /// </summary>
    public string OrganizationId { get; set; }

    public string OrganizationName { get; set; }

    public virtual bool IsOrganizationMapping => string.IsNullOrEmpty(UserId) && !string.IsNullOrEmpty(OrganizationId);

    public virtual object Clone()
    {
        return MemberwiseClone();
    }
}
