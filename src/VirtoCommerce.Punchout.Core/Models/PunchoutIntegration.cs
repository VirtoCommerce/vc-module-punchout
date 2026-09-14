using System;
using System.Collections.Generic;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.Punchout.Core.Models;

public class PunchoutIntegration : AuditableEntity, ICloneable
{
    public bool IsActive { get; set; }

    public string Name { get; set; }

    public string StoreId { get; set; }

    public string CredentialDomain { get; set; }

    public string SenderIdentity { get; set; }

    // For one-time generatoin for GetNew action only, not mapped to DB or passed via API for existing entities
    public string SharedSecret { get; set; }

    //public string SharedSecretHash { get; set; }

    //public string OrganizationId { get; set; }

    public IList<string> AllowedReturnUrls { get; set; }

    public virtual object Clone()
    {
        return MemberwiseClone();
    }
}
