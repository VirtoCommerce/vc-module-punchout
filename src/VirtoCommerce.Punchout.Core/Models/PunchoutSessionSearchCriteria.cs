using System.Collections.Generic;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.Punchout.Core.Models;

public class PunchoutSessionSearchCriteria : SearchCriteriaBase
{
    public string StoreId { get; set; }

    public string UserId { get; set; }

    /// <summary>
    /// The token the storefront receives in the start page URL.
    /// </summary>
    public string SessionToken { get; set; }

    public IList<string> Statuses { get; set; }

    /// <summary>
    /// Keeps only the sessions that are still usable right now: the ones with no expiration date
    /// or with one that has not passed yet.
    /// </summary>
    public bool NotExpired { get; set; }
}
