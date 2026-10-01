using System.Collections.Generic;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.Punchout.Core.Models;

public class PunchoutUserMappingSearchCriteria : SearchCriteriaBase
{
    /// <summary>
    /// cXML Header/Sender/Credential/Identity
    /// </summary>
    public IList<string> ExternalIds { get; set; }

    public IList<string> UserIds { get; set; }

    public IList<string> MemberIds { get; set; }

    public bool? IsActive { get; set; }
}
