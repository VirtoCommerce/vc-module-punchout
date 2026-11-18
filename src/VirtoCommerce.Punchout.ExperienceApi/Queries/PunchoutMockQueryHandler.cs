using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.Xapi.Core.Infrastructure;

namespace VirtoCommerce.Punchout.ExperienceApi.Queries;

public class PunchoutMockQueryHandler : IQueryHandler<PunchoutMockQuery, PunchoutMockResult>
{
    public Task<PunchoutMockResult> Handle(PunchoutMockQuery request, CancellationToken cancellationToken)
    {
        return Task.FromResult(new PunchoutMockResult());
    }
}
