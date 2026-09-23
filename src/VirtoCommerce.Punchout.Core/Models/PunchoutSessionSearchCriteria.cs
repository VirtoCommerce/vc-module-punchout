using System.Collections.Generic;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.Punchout.Core.Models;

public class PunchoutSessionSearchCriteria : SearchCriteriaBase
{
    public string StoreId { get; set; }

    public string UserId { get; set; }

    public string SessionToken { get; set; }

    public IList<string> Statuses { get; set; }

    // A session with no expiration date never counts as expired.
    public bool? Expired { get; set; }
}
