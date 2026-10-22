using GraphQL.Types;
using VirtoCommerce.Punchout.Core.Models;
using VirtoCommerce.Xapi.Core.Infrastructure;

namespace VirtoCommerce.Punchout.ExperienceApi.Commands;

public class CreatePunchoutRequisitionCommand : ICommand<PunchoutCheckoutResult>
{
    public string SessionId { get; set; }

    public string StoreId { get; set; }

    public string CultureName { get; set; }

    public string CurrencyCode { get; set; }

    public string UserId { get; set; }

    public string OrganizationId { get; set; }
}

public class CreatePunchoutRequisitionCommandType : InputObjectGraphType
{
    public CreatePunchoutRequisitionCommandType()
    {
        Field<NonNullGraphType<StringGraphType>>("storeId");
        Field<StringGraphType>("currencyCode");
        Field<StringGraphType>("cultureName");
    }
}
