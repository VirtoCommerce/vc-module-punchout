using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.Punchout.Core.Models;

public class PunchoutOrderMessageSearchCriteria : SearchCriteriaBase
{
    public string SessionId { get; set; }
}
