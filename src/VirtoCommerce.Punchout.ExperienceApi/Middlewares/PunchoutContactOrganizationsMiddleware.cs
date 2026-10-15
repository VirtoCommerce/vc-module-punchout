using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using PipelineNet.Middleware;
using VirtoCommerce.ProfileExperienceApiModule.Data.Models;
using VirtoCommerce.Punchout.Core;

namespace VirtoCommerce.Punchout.ExperienceApi.Middlewares;

public class PunchoutContactOrganizationsMiddleware : IAsyncMiddleware<ContactOrganizationsContext>
{
    public async Task Run(ContactOrganizationsContext parameter, Func<ContactOrganizationsContext, Task> next)
    {
        if (parameter.Principal?.FindFirstValue(ModuleConstants.Security.Claims.ChannelId) == ModuleConstants.Security.PunchoutGrantType)
        {
            parameter.DestinationOrganizationIds = parameter.SourceOrganizationIds
                .Where(id => id == parameter.CurrentOrganizationId)
                .ToList();
        }

        await next(parameter);
    }
}
