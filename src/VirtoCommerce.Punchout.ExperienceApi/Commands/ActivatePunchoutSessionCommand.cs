using GraphQL.Types;
using VirtoCommerce.Punchout.Core.Models;
using VirtoCommerce.Xapi.Core.Infrastructure;

namespace VirtoCommerce.Punchout.ExperienceApi.Commands;

public class ActivatePunchoutSessionCommand : ICommand<PunchoutSessonActivationResult>
{
    public string StoreId { get; set; }

    public string SessionToken { get; set; }

    public string CultureName { get; set; }

    public string CurrencyCode { get; set; }

    public string UserId { get; set; }

    public string OrganizationId { get; set; }
}

public class ActivatePunchoutSessionCommandType : InputObjectGraphType
{
    public ActivatePunchoutSessionCommandType()
    {
        Field<NonNullGraphType<StringGraphType>>("storeId");
        Field<NonNullGraphType<StringGraphType>>("SessionToken");
        Field<StringGraphType>("currencyCode");
        Field<StringGraphType>("cultureName");
    }
}
