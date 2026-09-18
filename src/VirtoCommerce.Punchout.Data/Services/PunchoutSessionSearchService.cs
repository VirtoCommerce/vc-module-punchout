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

public class PunchoutSessionSearchService(
    Func<IPunchoutRepository> repositoryFactory,
    IPlatformMemoryCache platformMemoryCache,
    IPunchoutSessionService crudService,
    IOptions<CrudOptions> crudOptions)
    : SearchService<PunchoutSessionSearchCriteria, PunchoutSessionSearchResult, PunchoutSession, PunchoutSessionEntity>
        (repositoryFactory, platformMemoryCache, crudService, crudOptions),
        IPunchoutSessionSearchService
{
    protected override IQueryable<PunchoutSessionEntity> BuildQuery(IRepository repository, PunchoutSessionSearchCriteria criteria)
    {
        var query = ((IPunchoutRepository)repository).PunchoutSessions;

        if (!string.IsNullOrEmpty(criteria.StoreId))
        {
            query = query.Where(x => x.StoreId == criteria.StoreId);
        }

        if (!string.IsNullOrEmpty(criteria.UserId))
        {
            query = query.Where(x => x.UserId == criteria.UserId);
        }

        if (!string.IsNullOrEmpty(criteria.SessionToken))
        {
            query = query.Where(x => x.SessionToken == criteria.SessionToken);
        }

        if (!criteria.Statuses.IsNullOrEmpty())
        {
            query = query.Where(x => criteria.Statuses.Contains(x.Status));
        }

        if (criteria.NotExpired)
        {
            var now = DateTime.UtcNow;

            query = query.Where(x => x.ExpirationDate == null || x.ExpirationDate > now);
        }

        return query;
    }

    protected override IList<SortInfo> BuildSortExpression(PunchoutSessionSearchCriteria criteria)
    {
        var sortInfos = criteria.SortInfos;

        if (sortInfos.IsNullOrEmpty())
        {
            sortInfos =
            [
                new SortInfo { SortColumn = nameof(PunchoutSessionEntity.CreatedDate), SortDirection = SortDirection.Descending },
                new SortInfo { SortColumn = nameof(PunchoutSessionEntity.Id) },
            ];
        }

        return sortInfos;
    }
}
