using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.Punchout.Core.Models;
using VirtoCommerce.Punchout.ExperienceApi.Schemas;
using VirtoCommerce.Xapi.Core.BaseQueries;

namespace VirtoCommerce.Punchout.ExperienceApi.Queries;

public class GetPunchoutCheckoutQueryBuilder : QueryBuilder<GetPunchoutCheckoutQuery, PunchoutCheckoutResult, PunchoutCheckoutType>
{
    protected override string Name => "punchoutCheckout";

    public GetPunchoutCheckoutQueryBuilder(IAuthorizationService authorizationService) : base(authorizationService)
    {
    }
}
