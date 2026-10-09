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
using VirtoCommerce.Platform.Security.OpenIddict;
using VirtoCommerce.Punchout.Core;
using VirtoCommerce.Punchout.Core.Models;
using VirtoCommerce.Punchout.Core.Services;

namespace VirtoCommerce.Punchout.Data.Services;

public class PunchoutGrantTypeHandler : GrantTypeHandlerBase
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IPunchoutSessionManagementService _sessionManagementService;
    private readonly IPunchoutUserMappingSearchService _userMappingSearchService;

    public PunchoutGrantTypeHandler(
        SignInManager<ApplicationUser> signInManager,
        IOptions<IdentityOptions> identityOptions,
        IEnumerable<ITokenRequestValidator> requestValidators,
        IEnumerable<ITokenClaimProvider> claimProviders,
        IEnumerable<ITokenRequestHandler> requestHandlers,
        IEventPublisher eventPublisher,
        IPunchoutSessionManagementService sessionManagementService,
        IPunchoutUserMappingSearchService userMappingSearchService)
        : base(signInManager, identityOptions, requestValidators, claimProviders, requestHandlers, eventPublisher)
    {
        _signInManager = signInManager;
        _sessionManagementService = sessionManagementService;
        _userMappingSearchService = userMappingSearchService;
    }

    public override string GrantType => ModuleConstants.Security.PunchoutGrantType;
    protected override string SignInType => ModuleConstants.Security.PunchoutGrantType;

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

        if (!await HasActiveUserMappingAsync(session))
        {
            return GrantValidationResult.Fail(SecurityErrorDescriber.LoginFailed());
        }

        var user = await _signInManager.UserManager.FindByIdAsync(session.UserId);
        if (user == null
            || user.IsAdministrator
            || user.UserType != nameof(UserType.Customer)
            || await _signInManager.UserManager.IsLockedOutAsync(user)
            || !await _signInManager.CanSignInAsync(user))
        {
            return GrantValidationResult.Fail(SecurityErrorDescriber.LoginFailed());
        }

        // set additional params to set to claims later
        context.AdditionalParameters.Add("channelSessionId", session.Id);
        context.AdditionalParameters.Add("sessionExpirationDate", session.ExpirationDate.Value);

        return GrantValidationResult.Succeed(user);
    }

    protected virtual async Task<bool> HasActiveUserMappingAsync(PunchoutSession session)
    {
        if (session.SenderIdentity.IsNullOrEmpty())
        {
            return false;
        }

        var criteria = AbstractTypeFactory<PunchoutUserMappingSearchCriteria>.TryCreateInstance();
        criteria.ExternalIds = [session.SenderIdentity];
        criteria.UserIds = [session.UserId];
        criteria.IsActive = true;
        criteria.Take = 0;

        var searchResult = await _userMappingSearchService.SearchNoCloneAsync(criteria);

        return searchResult.TotalCount > 0;
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
        principal.SetClaim("channelId", "punchout");
        if (context.AdditionalParameters.TryGetValue("channelSessionId", out var channelSessionId))
        {
            principal.SetClaim("channelSessionId", (string)channelSessionId);
        }
        if (context.AdditionalParameters.TryGetValue("sessionExpirationDate", out var sessionExpirationDate))
        {
            principal.SetAccessTokenLifetime((DateTime)sessionExpirationDate - DateTime.UtcNow);
        }
    }
}
