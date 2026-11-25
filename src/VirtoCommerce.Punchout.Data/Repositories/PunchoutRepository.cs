using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Domain;
using VirtoCommerce.Platform.Data.Infrastructure;
using VirtoCommerce.Punchout.Core;
using VirtoCommerce.Punchout.Data.Models;

namespace VirtoCommerce.Punchout.Data.Repositories;

public class PunchoutRepository(PunchoutDbContext dbContext, IUnitOfWork unitOfWork = null)
    : DbContextRepositoryBase<PunchoutDbContext>(dbContext, unitOfWork),
        IPunchoutRepository
{
    public IQueryable<PunchoutSessionEntity> PunchoutSessions => DbContext.Set<PunchoutSessionEntity>();

    public IQueryable<PunchoutUserMappingEntity> PunchoutUserMappings => DbContext.Set<PunchoutUserMappingEntity>();

    public IQueryable<PunchoutOrderMessageEntity> PunchoutOrderMessages => DbContext.Set<PunchoutOrderMessageEntity>();

    public IQueryable<PunchoutOrderRequestEntity> PunchoutOrderRequests => DbContext.Set<PunchoutOrderRequestEntity>();

    public IQueryable<PunchoutOrderLineEntity> PunchoutOrderLines => DbContext.Set<PunchoutOrderLineEntity>();

    public IQueryable<PunchoutRequisitionItemEntity> PunchoutRequisitionItems => DbContext.Set<PunchoutRequisitionItemEntity>();

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

    public virtual async Task<IList<PunchoutOrderRequestEntity>> GetPunchoutOrderRequestsByIdsAsync(IList<string> ids, string responseGroup)
    {
        if (ids.IsNullOrEmpty())
        {
            return [];
        }

        return ids.Count == 1
            ? await PunchoutOrderRequests.Where(x => x.Id == ids.First()).ToListAsync()
            : await PunchoutOrderRequests.Where(x => ids.Contains(x.Id)).ToListAsync();
    }

    public virtual async Task<IList<PunchoutOrderMessageEntity>> GetPunchoutOrderMessagesByIdsAsync(IList<string> ids, string responseGroup)
    {
        if (ids.IsNullOrEmpty())
        {
            return [];
        }

        var query = ids.Count == 1
            ? PunchoutOrderMessages.Where(x => x.Id == ids.First())
            : PunchoutOrderMessages.Where(x => ids.Contains(x.Id));

        return await query.Include(x => x.Items).ToListAsync();
    }

    public virtual async Task<IList<PunchoutRequisitionItemEntity>> GetReturnedRequisitionItemsAsync(string configurationId, string userId, string organizationId,
        IList<string> lineItemIds)
    {
        if (string.IsNullOrEmpty(configurationId) || (string.IsNullOrEmpty(userId) && string.IsNullOrEmpty(organizationId)) || lineItemIds.IsNullOrEmpty())
        {
            return [];
        }

        var returnedSessions = PunchoutSessions.Where(x =>
            x.ConfigurationId == configurationId &&
            x.Status == ModuleConstants.SessionStatus.Returned);

        // The sessions of an organization mapping are shared by the users of the organization,
        // the sessions of a user mapping belong to the user only
        returnedSessions = string.IsNullOrEmpty(organizationId)
            ? returnedSessions.Where(x => x.UserId == userId && x.OrganizationId == null)
            : returnedSessions.Where(x => x.OrganizationId == organizationId);

        return await PunchoutRequisitionItems
            .Where(x => lineItemIds.Contains(x.LineItemId))
            .Join(returnedSessions, item => item.SessionId, session => session.Id, (item, _) => item)
            .AsNoTracking()
            .ToListAsync();
    }

    public virtual async Task<string> RedeemSessionTokenAsync(string sessionTokenHash, DateTime now)
    {
        if (sessionTokenHash.IsNullOrEmpty())
        {
            return null;
        }

        var sessionId = await PunchoutSessions
            .Where(x => x.SessionTokenHash == sessionTokenHash)
            .Select(x => x.Id)
            .FirstOrDefaultAsync();

        if (sessionId is null)
        {
            return null;
        }

        // Atomic conditional update
        var affected = await DbContext.Set<PunchoutSessionEntity>()
            .Where(x => x.Id == sessionId &&
                        !x.IsSessionTokenRedeemed &&
                        x.TokenExpirationDate != null && x.TokenExpirationDate > now)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.IsSessionTokenRedeemed, true)
                .SetProperty(x => x.ModifiedDate, now));

        return affected == 1 ? sessionId : null;
    }

    public virtual async Task<bool> ReturnSessionAsync(string sessionId, DateTime now)
    {
        if (sessionId.IsNullOrEmpty())
        {
            return false;
        }

        // Atomic conditional update
        var affected = await DbContext.Set<PunchoutSessionEntity>()
            .Where(x => x.Id == sessionId &&
                        x.Status == ModuleConstants.SessionStatus.Active &&
                        x.ExpirationDate != null && x.ExpirationDate > now)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, ModuleConstants.SessionStatus.Returned)
                .SetProperty(x => x.ModifiedDate, now));

        return affected == 1;
    }

    public virtual async Task<IList<PunchoutOrderLineEntity>> GetPunchoutOrderLinesByIdsAsync(IList<string> ids, string responseGroup)
    {
        if (ids.IsNullOrEmpty())
        {
            return [];
        }

        return ids.Count == 1
            ? await PunchoutOrderLines.Where(x => x.Id == ids.First()).ToListAsync()
            : await PunchoutOrderLines.Where(x => ids.Contains(x.Id)).ToListAsync();
    }
}
