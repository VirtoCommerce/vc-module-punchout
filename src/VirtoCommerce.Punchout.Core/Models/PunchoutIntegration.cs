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
    /// The plain shared secret. Generated once by the GetNew action and accepted on save, where it is SharedSecretHash.
    /// Never stored and never returned for an existing integration.
    /// </summary>
    public string SharedSecret { get; set; }

    /// <summary>
    /// A salted one-way hash of SharedSecret. Stored in the database, never exposed through the API.
    /// </summary>
    public string SharedSecretHash { get; set; }

    public IList<string> AllowedReturnUrls { get; set; }

    /// <summary>
    /// The organization this integration serves. An organization can have several integrations,
    /// but an integration belongs to exactly one organization.
    /// </summary>
    public string OrganizationId { get; set; }

    public virtual object Clone()
    {
        var result = (PunchoutIntegration)MemberwiseClone();

        result.AllowedReturnUrls = AllowedReturnUrls?.ToList();

        return result;
    }
}
