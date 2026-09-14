using System.Collections.Generic;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.Punchout.Core.Models;

public class PunchoutIntegrationSearchCriteria : SearchCriteriaBase
{
    /// <summary>
    /// Returns only the integrations linked to at least one of these organizations.
    /// </summary>
    public IList<string> OrganizationIds { get; set; }
}
