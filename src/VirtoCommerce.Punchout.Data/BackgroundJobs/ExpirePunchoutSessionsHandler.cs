using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Punchout.Core;
using VirtoCommerce.Punchout.Core.Models;
using VirtoCommerce.Punchout.Core.Services;

namespace VirtoCommerce.Punchout.Data.BackgroundJobs;

public class ExpirePunchoutSessionsHandler(
    IPunchoutSessionSearchService searchService,
    IPunchoutSessionService crudService,
    ILogger<ExpirePunchoutSessionsHandler> logger)
    : IExpirePunchoutSessionsHandler
{
    protected virtual int BatchSize => 50;

    protected virtual int MaxBatches => 100;

    public virtual async Task ExpireSessionsAsync()
    {
        var total = 0;

        for (var i = 0; i < MaxBatches; i++)
        {
            var sessions = await FindExpiredSessionsAsync();

            if (sessions.Count == 0)
            {
                break;
            }

            foreach (var session in sessions)
            {
                session.Status = ModuleConstants.SessionStatus.Expired;
            }

            await crudService.SaveChangesAsync(sessions);

            total += sessions.Count;

            if (sessions.Count < BatchSize)
            {
                break;
            }
        }

        if (total > 0)
        {
            logger.LogInformation("Expired {Count} punchout session(s).", total);
        }
    }

    protected virtual async Task<IList<PunchoutSession>> FindExpiredSessionsAsync()
    {
        var criteria = AbstractTypeFactory<PunchoutSessionSearchCriteria>.TryCreateInstance();
        criteria.Statuses = [ModuleConstants.SessionStatus.Created, ModuleConstants.SessionStatus.Active];
        criteria.Expired = true;
        criteria.Take = BatchSize;

        var searchResult = await searchService.SearchAsync(criteria);

        return searchResult.Results;
    }
}
