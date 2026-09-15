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

public class PunchoutIntegrationSearchService(
    Func<IPunchoutRepository> repositoryFactory,
    IPlatformMemoryCache platformMemoryCache,
    IPunchoutIntegrationService crudService,
    IOptions<CrudOptions> crudOptions)
    : SearchService<PunchoutIntegrationSearchCriteria, PunchoutIntegrationSearchResult, PunchoutIntegration, PunchoutIntegrationEntity>
        (repositoryFactory, platformMemoryCache, crudService, crudOptions),
        IPunchoutIntegrationSearchService
{
    protected override IQueryable<PunchoutIntegrationEntity> BuildQuery(IRepository repository, PunchoutIntegrationSearchCriteria criteria)
    {
        var query = ((IPunchoutRepository)repository).PunchoutIntegrations;

        if (!string.IsNullOrEmpty(criteria.SenderIdentity))
        {
            query = query.Where(x => x.SenderIdentity == criteria.SenderIdentity);
        }

        if (criteria.IsActive != null)
        {
            query = query.Where(x => x.IsActive == criteria.IsActive);
        }

        if (!criteria.OrganizationIds.IsNullOrEmpty())
        {
            query = query.Where(x => criteria.OrganizationIds.Contains(x.OrganizationId));
        }

        return query;
    }

    protected override IList<SortInfo> BuildSortExpression(PunchoutIntegrationSearchCriteria criteria)
    {
        var sortInfos = criteria.SortInfos;

        if (sortInfos.IsNullOrEmpty())
        {
            sortInfos =
            [
                new SortInfo { SortColumn = nameof(PunchoutIntegrationEntity.CreatedDate), SortDirection = SortDirection.Descending },
                new SortInfo { SortColumn = nameof(PunchoutIntegrationEntity.Id) },
            ];
        }

        return sortInfos;
    }
}
