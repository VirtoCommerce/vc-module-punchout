using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Security.SignInLog;
using VirtoCommerce.Platform.Security;
using VirtoCommerce.Platform.Security.OpenIddict;
using VirtoCommerce.Punchout.Core;

namespace VirtoCommerce.Punchout.Data.Services;

/// <summary>
/// Keeps a punchout session in the organization of its organization mapping.
/// The Customer module's validator falls back to another organization of the user when the requested one is locked or blocked,
/// a punchout session must not switch to another organization instead.
/// </summary>
public class PunchoutOrganizationRequestValidator : ITokenRequestValidator
{
    /// <summary>
    /// Runs after the Customer module's organization validator (priority 50), validators run by descending priority.
    /// </summary>
    public virtual int Priority { get; set; }

    public virtual Task<IList<TokenResponse>> ValidateAsync(TokenRequestContext context)
    {
        if (context.Request?.GrantType != ModuleConstants.Security.PunchoutGrantType ||
            !context.AdditionalParameters.TryGetValue(ModuleConstants.Security.Parameters.SessionOrganizationId, out var sessionOrganizationId))
        {
            return Task.FromResult<IList<TokenResponse>>([]);
        }

        var organizationId = context.Request.GetParameter(ModuleConstants.Security.Parameters.OrganizationId)?.ToString();

        if (organizationId.EqualsIgnoreCase((string)sessionOrganizationId))
        {
            return Task.FromResult<IList<TokenResponse>>([]);
        }

        context.FailureReason = SignInFailureReason.NotAllowed;

        return Task.FromResult<IList<TokenResponse>>([SecurityErrorDescriber.LoginFailed()]);
    }
}
