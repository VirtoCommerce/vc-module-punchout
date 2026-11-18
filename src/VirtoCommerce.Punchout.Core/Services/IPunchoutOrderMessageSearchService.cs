using VirtoCommerce.Platform.Core.GenericCrud;
using VirtoCommerce.Punchout.Core.Models;

namespace VirtoCommerce.Punchout.Core.Services;

public interface IPunchoutOrderMessageSearchService : ISearchService<PunchoutOrderMessageSearchCriteria, PunchoutOrderMessageSearchResult, PunchoutOrderMessage>;
