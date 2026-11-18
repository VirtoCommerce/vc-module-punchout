using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.Xapi.Core.BaseQueries;

namespace VirtoCommerce.Punchout.ExperienceApi.Queries;

public class PunchoutMockQueryBuilder : QueryBuilder<PunchoutMockQuery, PunchoutMockResult, PunchoutResultType>
{
    protected override string Name => "punchoutMockQuery";

    public PunchoutMockQueryBuilder(IAuthorizationService authorizationService) : base(authorizationService)
    {
    }
}
