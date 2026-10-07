using GraphQL;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.Punchout.Core.Models;
using VirtoCommerce.Punchout.ExperienceApi.Extensions;
using VirtoCommerce.Punchout.ExperienceApi.Schemas;
using VirtoCommerce.Xapi.Core.BaseQueries;
using VirtoCommerce.Xapi.Core.Extensions;

namespace VirtoCommerce.Punchout.ExperienceApi.Commands;

public class CreatePunchoutRequisitionCommandBuilder : CommandBuilder<CreatePunchoutRequisitionCommand, PunchoutCheckoutResult, CreatePunchoutRequisitionCommandType, PunchoutCheckoutType>
{
    protected override string Name => "createPunchoutRequisition";

    public CreatePunchoutRequisitionCommandBuilder(IAuthorizationService authorizationService)
        : base(authorizationService)
    {
    }

    protected override CreatePunchoutRequisitionCommand GetRequest(IResolveFieldContext<object> context)
    {
        var request = base.GetRequest(context);

        request.SessionId = context.GetCurrentPunchoutSessionId();
        request.UserId = context.GetCurrentUserId();
        request.OrganizationId = context.GetCurrentOrganizationId();

        return request;
    }
}
