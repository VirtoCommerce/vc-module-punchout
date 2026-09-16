using GraphQL;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.Punchout.Core.Models;
using VirtoCommerce.Punchout.ExperienceApi.Schemas;
using VirtoCommerce.Xapi.Core.BaseQueries;
using VirtoCommerce.Xapi.Core.Extensions;

namespace VirtoCommerce.Punchout.ExperienceApi.Commands;

public class ActivatePunchoutSessionCommandBuilder : CommandBuilder<ActivatePunchoutSessionCommand, PunchoutSessionActivationResult, ActivatePunchoutSessionCommandType, PunchoutSessonActivationResultType>
{
    protected override string Name => "activatePunchoutSession";

    public ActivatePunchoutSessionCommandBuilder(IAuthorizationService authorizationService)
        : base(authorizationService)
    {
    }

    protected override ActivatePunchoutSessionCommand GetRequest(IResolveFieldContext<object> context)
    {
        var request = base.GetRequest(context);

        request.UserId = context.GetCurrentUserId();
        request.OrganizationId = context.GetCurrentOrganizationId();

        return request;
    }
}
