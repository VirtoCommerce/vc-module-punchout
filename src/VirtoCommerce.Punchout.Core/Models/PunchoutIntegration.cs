using System;
using System.Collections.Generic;
using System.Linq;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.Punchout.Core.Models;

public class PunchoutIntegration : AuditableEntity, ICloneable
{
    public bool IsActive { get; set; }

    public string Name { get; set; }

    public string StoreId { get; set; }

    public string CredentialDomain { get; set; }

    public string SenderIdentity { get; set; }

    /// <summary>
    /// The plain shared secret. Generated once by the GetNew action and accepted on save, where it is
    /// replaced with <see cref="SharedSecretHash"/>. Never stored and never returned for an existing integration.
    /// </summary>
    public string SharedSecret { get; set; }

    /// <summary>
    /// A salted one-way hash of <see cref="SharedSecret"/>. Stored in the database, never exposed through the API.
    /// </summary>
    public string SharedSecretHash { get; set; }

    public IList<string> AllowedReturnUrls { get; set; }

    /// <summary>
    /// Organizations this integration is available for. An organization can have several integrations,
    /// and an integration can serve several organizations.
    /// </summary>
    public IList<string> OrganizationIds { get; set; }

    public virtual object Clone()
    {
        var result = (PunchoutIntegration)MemberwiseClone();

        // Deep copy the collections: the CRUD service hands out clones of cached models, and a shared
        // list reference would let a caller modify the cached instance.
        result.AllowedReturnUrls = AllowedReturnUrls?.ToList();
        result.OrganizationIds = OrganizationIds?.ToList();

        return result;
    }
}
