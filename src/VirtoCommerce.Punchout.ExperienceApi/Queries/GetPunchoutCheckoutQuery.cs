using System.Collections.Generic;
using GraphQL;
using GraphQL.Types;
using VirtoCommerce.Punchout.Core.Models;
using VirtoCommerce.Xapi.Core.BaseQueries;
using VirtoCommerce.Xapi.Core.Extensions;

namespace VirtoCommerce.Punchout.ExperienceApi.Queries;

public class GetPunchoutCheckoutQuery : Query<PunchoutCheckoutResult>
{
    public string SessionId { get; set; }

    public string StoreId { get; set; }

    public string CultureName { get; set; }

    public string CurrencyCode { get; set; }

    public string UserId { get; set; }

    public string OrganizationId { get; set; }

    public override IEnumerable<QueryArgument> GetArguments()
    {
        yield return Argument<NonNullGraphType<StringGraphType>>(nameof(SessionId));
        yield return Argument<NonNullGraphType<StringGraphType>>(nameof(StoreId));
        yield return Argument<StringGraphType>(nameof(CultureName));
        yield return Argument<StringGraphType>(nameof(CurrencyCode));
    }

    public override void Map(IResolveFieldContext context)
    {
        StoreId = context.GetArgument<string>(nameof(StoreId));
        CultureName = context.GetArgument<string>(nameof(CultureName));
        CurrencyCode = context.GetArgument<string>(nameof(CurrencyCode));
        SessionId = context.GetArgument<string>(nameof(SessionId));
        UserId = context.GetCurrentUserId();
        OrganizationId = context.GetCurrentOrganizationId();
    }
}
