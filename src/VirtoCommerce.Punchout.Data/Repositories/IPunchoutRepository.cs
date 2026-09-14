using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Punchout.Data.Models;

namespace VirtoCommerce.Punchout.Data.Repositories;

public interface IPunchoutRepository : IRepository
{
    IQueryable<PunchoutSessionEntity> PunchoutSessions { get; }

    IQueryable<PunchoutIntegrationEntity> PunchoutIntegrations { get; }

    IQueryable<PunchoutIntegrationOrganizationEntity> PunchoutIntegrationOrganizations { get; }

    Task<IList<PunchoutSessionEntity>> GetPunchoutSessionsByIdsAsync(IList<string> ids, string responseGroup);

    Task<IList<PunchoutIntegrationEntity>> GetPunchoutIntegrationsByIdsAsync(IList<string> ids, string responseGroup);
}
