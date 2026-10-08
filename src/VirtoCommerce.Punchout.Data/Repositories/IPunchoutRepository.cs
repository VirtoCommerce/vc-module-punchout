using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Punchout.Data.Models;

namespace VirtoCommerce.Punchout.Data.Repositories;

public interface IPunchoutRepository : IRepository
{
    IQueryable<PunchoutSessionEntity> PunchoutSessions { get; }

    IQueryable<PunchoutUserMappingEntity> PunchoutUserMappings { get; }

    IQueryable<PunchoutOrderMessageEntity> PunchoutOrderMessages { get; }

    Task<IList<PunchoutSessionEntity>> GetPunchoutSessionsByIdsAsync(IList<string> ids, string responseGroup);

    Task<IList<PunchoutUserMappingEntity>> GetPunchoutUserMappingsByIdsAsync(IList<string> ids, string responseGroup);

    Task<IList<PunchoutOrderMessageEntity>> GetPunchoutOrderMessagesByIdsAsync(IList<string> ids, string responseGroup);

    /// <summary>
    /// Atomically marks the session token as redeemed.
    /// </summary>
    /// <returns>The session id, or null if the token is invalid, already redeemed or expired</returns>
    Task<string> RedeemSessionTokenAsync(string sessionTokenHash, DateTime now);
}
