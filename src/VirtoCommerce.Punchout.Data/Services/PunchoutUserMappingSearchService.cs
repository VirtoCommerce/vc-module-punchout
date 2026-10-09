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

public class PunchoutUserMappingSearchService(
    Func<IPunchoutRepository> repositoryFactory,
    IPlatformMemoryCache platformMemoryCache,
    IPunchoutUserMappingService crudService,
    IOptions<CrudOptions> crudOptions)
    : SearchService<PunchoutUserMappingSearchCriteria, PunchoutUserMappingSearchResult, PunchoutUserMapping, PunchoutUserMappingEntity>
        (repositoryFactory, platformMemoryCache, crudService, crudOptions),
        IPunchoutUserMappingSearchService
{
    protected override IQueryable<PunchoutUserMappingEntity> BuildQuery(IRepository repository, PunchoutUserMappingSearchCriteria criteria)
    {
        var query = ((IPunchoutRepository)repository).PunchoutUserMappings;

        if (!criteria.ExternalIds.IsNullOrEmpty())
        {
            query = query.Where(x => criteria.ExternalIds.Contains(x.ExternalId));
        }

        if (!criteria.UserIds.IsNullOrEmpty())
        {
            query = query.Where(x => criteria.UserIds.Contains(x.UserId));
        }

        if (!criteria.MemberIds.IsNullOrEmpty())
        {
            query = query.Where(x => criteria.MemberIds.Contains(x.MemberId));
        }

        if (criteria.IsActive != null)
        {
            query = query.Where(x => x.IsActive == criteria.IsActive);
        }

        if (!string.IsNullOrEmpty(criteria.Keyword))
        {
            query = query.Where(x => x.ExternalId.Contains(criteria.Keyword) || x.UserName.Contains(criteria.Keyword));
        }

        return query;
    }

    protected override IList<SortInfo> BuildSortExpression(PunchoutUserMappingSearchCriteria criteria)
    {
        var sortInfos = criteria.SortInfos;

        if (sortInfos.IsNullOrEmpty())
        {
            sortInfos =
            [
                new SortInfo { SortColumn = nameof(PunchoutUserMappingEntity.CreatedDate), SortDirection = SortDirection.Descending },
                new SortInfo { SortColumn = nameof(PunchoutUserMappingEntity.Id) },
            ];
        }

        return sortInfos;
    }
}
