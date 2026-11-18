using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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

/// <summary>
/// The ordered items decide which purchase order may order a requisition item, the search always reads the database.
/// </summary>
public class PunchoutOrderLineSearchService(
    Func<IPunchoutRepository> repositoryFactory,
    IPlatformMemoryCache platformMemoryCache,
    IPunchoutOrderLineService crudService,
    IOptions<CrudOptions> crudOptions)
    : SearchService<PunchoutOrderLineSearchCriteria, PunchoutOrderLineSearchResult, PunchoutOrderLine, PunchoutOrderLineEntity>
        (repositoryFactory, platformMemoryCache, crudService, crudOptions),
        IPunchoutOrderLineSearchService
{
    public override async Task<PunchoutOrderLineSearchResult> SearchAsync(PunchoutOrderLineSearchCriteria criteria, bool clone = true)
    {
        using (CacheDisabler.DisableCache())
        {
            return await base.SearchAsync(criteria, clone);
        }
    }

    protected override IQueryable<PunchoutOrderLineEntity> BuildQuery(IRepository repository, PunchoutOrderLineSearchCriteria criteria)
    {
        var query = ((IPunchoutRepository)repository).PunchoutOrderLines;

        if (!string.IsNullOrEmpty(criteria.ConfigurationId))
        {
            query = query.Where(x => x.ConfigurationId == criteria.ConfigurationId);
        }

        if (!string.IsNullOrEmpty(criteria.PurchaseOrderNumber))
        {
            query = query.Where(x => x.PurchaseOrderNumber == criteria.PurchaseOrderNumber);
        }

        if (!criteria.LineItemIds.IsNullOrEmpty())
        {
            query = query.Where(x => criteria.LineItemIds.Contains(x.LineItemId));
        }

        return query;
    }

    protected override IList<SortInfo> BuildSortExpression(PunchoutOrderLineSearchCriteria criteria)
    {
        var sortInfos = criteria.SortInfos;

        if (sortInfos.IsNullOrEmpty())
        {
            sortInfos =
            [
                new SortInfo { SortColumn = nameof(PunchoutOrderLineEntity.CreatedDate) },
                new SortInfo { SortColumn = nameof(PunchoutOrderLineEntity.Id) },
            ];
        }

        return sortInfos;
    }
}
