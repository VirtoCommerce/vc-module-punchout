using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Options;
using VirtoCommerce.Platform.Core.Caching;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.GenericCrud;
using VirtoCommerce.Platform.Data.GenericCrud;
using VirtoCommerce.Punchout.Core.Models;
using VirtoCommerce.Punchout.Core.Services;
using VirtoCommerce.Punchout.Data.Models;
using VirtoCommerce.Punchout.Data.Repositories;

namespace VirtoCommerce.Punchout.Data.Services;

public class PunchoutOrderRequestSearchService(
    Func<IPunchoutRepository> repositoryFactory,
    IPlatformMemoryCache platformMemoryCache,
    IPunchoutOrderRequestService crudService,
    IOptions<CrudOptions> crudOptions)
    : SearchService<PunchoutOrderRequestSearchCriteria, PunchoutOrderRequestSearchResult, PunchoutOrderRequest, PunchoutOrderRequestEntity>
        (repositoryFactory, platformMemoryCache, crudService, crudOptions),
        IPunchoutOrderRequestSearchService
{
    protected override IQueryable<PunchoutOrderRequestEntity> BuildQuery(IRepository repository, PunchoutOrderRequestSearchCriteria criteria)
    {
        var query = ((IPunchoutRepository)repository).PunchoutOrderRequests;
        return query;
    }

    protected override IList<SortInfo> BuildSortExpression(PunchoutOrderRequestSearchCriteria criteria)
    {
        var sortInfos = criteria.SortInfos;

        if (sortInfos.IsNullOrEmpty())
        {
            sortInfos =
            [
                new SortInfo { SortColumn = nameof(PunchoutOrderRequestEntity.CreatedDate), SortDirection = SortDirection.Descending },
                new SortInfo { SortColumn = nameof(PunchoutOrderRequestEntity.Id) },
            ];
        }

        return sortInfos;
    }
}
