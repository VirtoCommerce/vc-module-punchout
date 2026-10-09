using System.Security.Claims;
using GraphQL;
using VirtoCommerce.Punchout.Core;
using VirtoCommerce.Xapi.Core.Extensions;

namespace VirtoCommerce.Punchout.ExperienceApi.Extensions;

public static class ResolveFieldContextExtensions
{
    /// <summary>
    /// The punchout session id from the access token issued by the punchout grant, or null for other tokens.
    /// </summary>
    public static string GetCurrentPunchoutSessionId(this IResolveFieldContext context)
    {
        var principal = context.GetCurrentPrincipal();

        return principal?.FindFirstValue(ModuleConstants.Security.Claims.ChannelId) == ModuleConstants.Security.PunchoutGrantType
            ? principal.FindFirstValue(ModuleConstants.Security.Claims.ChannelSessionId)
            : null;
    }
}
