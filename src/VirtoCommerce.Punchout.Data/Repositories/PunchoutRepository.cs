using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Domain;
using VirtoCommerce.Platform.Data.Infrastructure;
using VirtoCommerce.Punchout.Data.Models;

namespace VirtoCommerce.Punchout.Data.Repositories;

public class PunchoutRepository(PunchoutDbContext dbContext, IUnitOfWork unitOfWork = null)
    : DbContextRepositoryBase<PunchoutDbContext>(dbContext, unitOfWork),
        IPunchoutRepository
{
    public IQueryable<PunchoutSessionEntity> PunchoutSessions => DbContext.Set<PunchoutSessionEntity>();

    public IQueryable<PunchoutIntegrationEntity> PunchoutIntegrations => DbContext.Set<PunchoutIntegrationEntity>();

    public IQueryable<PunchoutUserMappingEntity> PunchoutUserMappings => DbContext.Set<PunchoutUserMappingEntity>();

    public virtual async Task<IList<PunchoutSessionEntity>> GetPunchoutSessionsByIdsAsync(IList<string> ids, string responseGroup)
    {
        if (ids.IsNullOrEmpty())
        {
            return [];
        }

        return ids.Count == 1
            ? await PunchoutSessions.Where(x => x.Id == ids.First()).ToListAsync()
            : await PunchoutSessions.Where(x => ids.Contains(x.Id)).ToListAsync();
    }

    public virtual async Task<IList<PunchoutIntegrationEntity>> GetPunchoutIntegrationsByIdsAsync(IList<string> ids, string responseGroup)
    {
        if (ids.IsNullOrEmpty())
        {
            return [];
        }

        return ids.Count == 1
            ? await PunchoutIntegrations.Where(x => x.Id == ids.First()).ToListAsync()
            : await PunchoutIntegrations.Where(x => ids.Contains(x.Id)).ToListAsync();
    }

    public virtual async Task<IList<PunchoutUserMappingEntity>> GetPunchoutUserMappingsByIdsAsync(IList<string> ids, string responseGroup)
    {
        if (ids.IsNullOrEmpty())
        {
            return [];
        }

        return ids.Count == 1
            ? await PunchoutUserMappings.Where(x => x.Id == ids.First()).ToListAsync()
            : await PunchoutUserMappings.Where(x => ids.Contains(x.Id)).ToListAsync();
    }
}
