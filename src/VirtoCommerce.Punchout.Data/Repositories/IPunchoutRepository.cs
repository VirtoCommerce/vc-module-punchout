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

    Task<IList<PunchoutSessionEntity>> GetPunchoutSessionsByIdsAsync(IList<string> ids, string responseGroup);

    Task<IList<PunchoutUserMappingEntity>> GetPunchoutUserMappingsByIdsAsync(IList<string> ids, string responseGroup);
}
