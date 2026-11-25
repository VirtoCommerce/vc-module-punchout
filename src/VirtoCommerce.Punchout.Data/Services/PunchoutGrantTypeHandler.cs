using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Events;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.Platform.Security;
using VirtoCommerce.Platform.Security.OpenIddict;
using VirtoCommerce.Punchout.Core;
using VirtoCommerce.Punchout.Core.Models;
using VirtoCommerce.Punchout.Core.Services;

namespace VirtoCommerce.Punchout.Data.Services;

public class PunchoutGrantTypeHandler : GrantTypeHandlerBase
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IPunchoutSessionManagementService _sessionManagementService;
    private readonly IPunchoutUserMappingService _userMappingService;
    private readonly IPunchoutOrganizationUserResolver _organizationUserResolver;
    private readonly IPunchoutUserPolicy _userPolicy;

    public PunchoutGrantTypeHandler(
        SignInManager<ApplicationUser> signInManager,
        IOptions<IdentityOptions> identityOptions,
        IEnumerable<ITokenRequestValidator> requestValidators,
        IEnumerable<ITokenClaimProvider> claimProviders,
        IEnumerable<ITokenRequestHandler> requestHandlers,
        IEventPublisher eventPublisher,
        ITwoFactorSignInService twoFactorSignInService,
        IPunchoutSessionManagementService sessionManagementService,
        IPunchoutUserMappingService userMappingService,
        IPunchoutOrganizationUserResolver organizationUserResolver,
        IPunchoutUserPolicy userPolicy)
        : base(signInManager, identityOptions, requestValidators, claimProviders, requestHandlers, eventPublisher, twoFactorSignInService)
    {
        _signInManager = signInManager;
        _sessionManagementService = sessionManagementService;
        _userMappingService = userMappingService;
        _organizationUserResolver = organizationUserResolver;
        _userPolicy = userPolicy;
    }

    public override string GrantType => ModuleConstants.Security.PunchoutGrantType;
    protected override string SignInType => ModuleConstants.Security.PunchoutGrantType;

    protected override Task<bool> IsTwoFactorRequiredAsync(TokenRequestContext context)
    {
        return Task.FromResult(false);
    }

    protected override async Task<GrantValidationResult> ValidateGrantAsync(TokenRequestContext context)
    {
        var sessionRedeemRequest = GetRedeemPunchoutSessionRequest(context);
        if (sessionRedeemRequest == null)
        {
            return GrantValidationResult.Fail(SecurityErrorDescriber.LoginFailed());
        }

        var sessionRedeemResult = await _sessionManagementService.RedeemSessionAsync(sessionRedeemRequest);

        var session = sessionRedeemResult.Session;
        if (session?.UserId == null || session.ExpirationDate == null)
        {
            return GrantValidationResult.Fail(SecurityErrorDescriber.LoginFailed());
        }

        var user = await _signInManager.UserManager.FindByIdAsync(session.UserId);
        if (user == null
            || !_userPolicy.IsAllowed(user)
            || await _signInManager.UserManager.IsLockedOutAsync(user)
            || !await _signInManager.CanSignInAsync(user))
        {
            return GrantValidationResult.Fail(SecurityErrorDescriber.LoginFailed());
        }

        if (!await HasActiveUserMappingAsync(session, user))
        {
            return GrantValidationResult.Fail(SecurityErrorDescriber.LoginFailed());
        }

        // set additional params to set to claims later
        context.AdditionalParameters.Add(ModuleConstants.Security.Claims.ChannelSessionId, session.Id);
        context.AdditionalParameters.Add("sessionExpirationDate", session.ExpirationDate.Value);

        if (!session.OrganizationId.IsNullOrEmpty())
        {
            // The session is bound to the organization of the organization mapping:
            // the Customer module validates the requested organization and sets it to the organization_id claim
            context.Request.SetParameter(ModuleConstants.Security.Parameters.OrganizationId, session.OrganizationId);
            context.AdditionalParameters.Add(ModuleConstants.Security.Parameters.SessionOrganizationId, session.OrganizationId);
        }

        return GrantValidationResult.Succeed(user);
    }

    /// <summary>
    /// The mapping the session was set up with is still active and still grants the session user.
    /// </summary>
    protected virtual async Task<bool> HasActiveUserMappingAsync(PunchoutSession session, ApplicationUser user)
    {
        if (session.UserMappingId.IsNullOrEmpty())
        {
            return false;
        }

        var mapping = await _userMappingService.GetNoCloneAsync(session.UserMappingId);

        if (mapping is not { IsActive: true })
        {
            return false;
        }

        if (mapping.IsOrganizationMapping)
        {
            return mapping.OrganizationId.EqualsIgnoreCase(session.OrganizationId) &&
                   await _organizationUserResolver.IsOrganizationUserAsync(user, mapping.OrganizationId);
        }

        return mapping.UserId.EqualsIgnoreCase(session.UserId);
    }

    protected virtual RedeemPunchoutSessionRequest GetRedeemPunchoutSessionRequest(TokenRequestContext context)
    {
        var sessionToken = (string)context.Request.GetParameter("session_token");

        if (sessionToken.IsNullOrEmpty())
        {
            return null;
        }

        var sessionRequest = AbstractTypeFactory<RedeemPunchoutSessionRequest>.TryCreateInstance();

        sessionRequest.SessionToken = sessionToken;

        return sessionRequest;
    }

    protected override void SetTicketScopes(TokenRequestContext context, ClaimsPrincipal principal)
    {
        // Explicitly no offline_access scope so no refresh_access is generated
        principal.SetScopes([]);

        // Set claims here (most convenient place)
        principal.SetClaim(ModuleConstants.Security.Claims.ChannelId, ModuleConstants.Security.PunchoutGrantType);
        if (context.AdditionalParameters.TryGetValue(ModuleConstants.Security.Claims.ChannelSessionId, out var channelSessionId))
        {
            principal.SetClaim(ModuleConstants.Security.Claims.ChannelSessionId, (string)channelSessionId);
        }
        if (context.AdditionalParameters.TryGetValue("sessionExpirationDate", out var sessionExpirationDate))
        {
            principal.SetAccessTokenLifetime((DateTime)sessionExpirationDate - DateTime.UtcNow);
        }
    }
}
