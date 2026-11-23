using System;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.Punchout.Core.Models;

/// <summary>
/// Links the identity a procurement system sends (Header/Sender/Credential/Identity by default) either to the platform user or to the organization.
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
    /// The contact the security account belongs to.
    /// </summary>
    public string MemberId { get; set; }

    /// <summary>
    /// Empty for a user mapping.
    /// </summary>
    public string OrganizationId { get; set; }

    public string OrganizationName { get; set; }

    public virtual bool IsOrganizationMapping => UserId.IsNullOrEmpty() && !OrganizationId.IsNullOrEmpty();

    public virtual object Clone()
    {
        return MemberwiseClone();
    }
}
