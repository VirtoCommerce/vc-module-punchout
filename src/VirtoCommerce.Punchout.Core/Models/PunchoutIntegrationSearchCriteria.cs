using System.Collections.Generic;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.Punchout.Core.Models;

public class PunchoutIntegrationSearchCriteria : SearchCriteriaBase
{
    public IList<string> OrganizationIds { get; set; }

    /// <summary>
    /// cXML Header/Sender/Credential/Identity
    /// </summary>
    public string SenderIdentity { get; set; }

    public bool? IsActive { get; set; }
}
