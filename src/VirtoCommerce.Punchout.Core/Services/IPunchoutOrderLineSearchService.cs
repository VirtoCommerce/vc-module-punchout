using VirtoCommerce.Platform.Core.GenericCrud;
using VirtoCommerce.Punchout.Core.Models;

namespace VirtoCommerce.Punchout.Core.Services;

public interface IPunchoutOrderLineSearchService : ISearchService<PunchoutOrderLineSearchCriteria, PunchoutOrderLineSearchResult, PunchoutOrderLine>;
