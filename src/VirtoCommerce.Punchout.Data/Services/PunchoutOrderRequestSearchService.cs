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

        if (!string.IsNullOrEmpty(criteria.ConfigurationId))
        {
            query = query.Where(x => x.ConfigurationId == criteria.ConfigurationId);
        }

        if (!string.IsNullOrEmpty(criteria.StoreId))
        {
            query = query.Where(x => x.StoreId == criteria.StoreId);
        }

        if (!string.IsNullOrEmpty(criteria.PayloadId))
        {
            query = query.Where(x => x.PayloadId == criteria.PayloadId);
        }

        if (!string.IsNullOrEmpty(criteria.PurchaseOrderNumber))
        {
            query = query.Where(x => x.PurchaseOrderNumber == criteria.PurchaseOrderNumber);
        }

        if (!string.IsNullOrEmpty(criteria.OrderType))
        {
            query = query.Where(x => x.OrderType == criteria.OrderType);
        }

        if (!string.IsNullOrEmpty(criteria.CustomerOrderId))
        {
            query = query.Where(x => x.CustomerOrderId == criteria.CustomerOrderId);
        }

        if (!criteria.Statuses.IsNullOrEmpty())
        {
            query = query.Where(x => criteria.Statuses.Contains(x.Status));
        }

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
