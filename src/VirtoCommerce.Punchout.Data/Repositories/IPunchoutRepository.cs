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

    IQueryable<PunchoutOrderRequestEntity> PunchoutOrderRequests { get; }

    IQueryable<PunchoutOrderLineEntity> PunchoutOrderLines { get; }

    IQueryable<PunchoutRequisitionItemEntity> PunchoutRequisitionItems { get; }

    Task<IList<PunchoutSessionEntity>> GetPunchoutSessionsByIdsAsync(IList<string> ids, string responseGroup);

    Task<IList<PunchoutUserMappingEntity>> GetPunchoutUserMappingsByIdsAsync(IList<string> ids, string responseGroup);

    Task<IList<PunchoutOrderMessageEntity>> GetPunchoutOrderMessagesByIdsAsync(IList<string> ids, string responseGroup);

    Task<IList<PunchoutOrderRequestEntity>> GetPunchoutOrderRequestsByIdsAsync(IList<string> ids, string responseGroup);

    /// <summary>
    /// The requisition items with the given cart line item ids from the sessions of the configuration that returned a requisition:
    /// the sessions of the organization mapping when the organization is set, otherwise the user mapping sessions of the user.
    /// </summary>
    Task<IList<PunchoutRequisitionItemEntity>> GetReturnedRequisitionItemsAsync(string configurationId, string userId, string organizationId, IList<string> lineItemIds);

    /// <summary>
    /// Atomically marks the session token as redeemed.
    /// </summary>
    /// <returns>The session id, or null if the token is invalid, already redeemed or expired</returns>
    Task<string> RedeemSessionTokenAsync(string sessionTokenHash, DateTime now);

    /// <summary>
    /// Atomically moves an active, not expired session to the Returned status.
    /// </summary>
    /// <returns>True if the session was returned by this call</returns>
    Task<bool> ReturnSessionAsync(string sessionId, DateTime now);

    Task<IList<PunchoutOrderLineEntity>> GetPunchoutOrderLinesByIdsAsync(IList<string> ids, string responseGroup);
}
