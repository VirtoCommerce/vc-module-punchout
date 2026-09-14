using VirtoCommerce.Platform.Core.GenericCrud;
using VirtoCommerce.Punchout.Core.Models;

namespace VirtoCommerce.Punchout.Core.Services;

public interface IPunchoutIntegrationSearchService : ISearchService<PunchoutIntegrationSearchCriteria, PunchoutIntegrationSearchResult, PunchoutIntegration>;
