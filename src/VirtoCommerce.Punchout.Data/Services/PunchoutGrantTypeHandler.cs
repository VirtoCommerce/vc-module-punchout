using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.Platform.Security.OpenIddict;
using VirtoCommerce.Punchout.Core;
using VirtoCommerce.Punchout.Core.Models;
using VirtoCommerce.Punchout.Core.Services;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace VirtoCommerce.Punchout.Data.Services;

public class PunchoutGrantTypeHandler : ITokenGrantTypeHandler
{
    private readonly IPunchoutSessionManagementService _sessionManagementService;
    private readonly Func<SignInManager<ApplicationUser>> _signInManagerFactory;
    private readonly IdentityOptions _identityOptions;

    public PunchoutGrantTypeHandler(
        IPunchoutSessionManagementService sessionManagementService,
        Func<SignInManager<ApplicationUser>> signInManagerFactory,
        IOptions<IdentityOptions> identityOptions)
    {
        _sessionManagementService = sessionManagementService;
        _signInManagerFactory = signInManagerFactory;
        _identityOptions = identityOptions.Value;
    }

    public string GrantType => ModuleConstants.Security.PunchoutGrantType;

    public async Task<TokenGrantTypeResult> HandleAsync(TokenRequestContext context)
    {
        var sessionRedeemRequest = GetReedeemPunchoutSessionRequest(context);
        if (sessionRedeemRequest == null)
        {
            return TokenGrantTypeResult.Fail(SecurityErrorDescriber.LoginFailed());
        }

        var sessionRedeemResult = await _sessionManagementService.RedeemSessionAsync(sessionRedeemRequest);

        var session = sessionRedeemResult.Session;
        if (session?.UserId == null || session.ExpirationDate == null)
        {
            return TokenGrantTypeResult.Fail(SecurityErrorDescriber.LoginFailed());
        }

        var signInManager = _signInManagerFactory();

        var user = await signInManager.UserManager.FindByIdAsync(session.UserId);
        if (user == null ||
            !await signInManager.CanSignInAsync(user) ||
            await signInManager.UserManager.IsLockedOutAsync(user))
        {
            return TokenGrantTypeResult.Fail(SecurityErrorDescriber.LoginFailed());
        }

        var principal = await signInManager.CreateUserPrincipalAsync(user);

        var ticket = await CreateTicket(principal, context, session);

        return TokenGrantTypeResult.Success(ticket);
    }

    protected virtual ReedeemPunchoutSessionRequest GetReedeemPunchoutSessionRequest(TokenRequestContext context)
    {
        var sessionToken = (string)context.Request.GetParameter("session_token");

        if (sessionToken.IsNullOrEmpty())
        {
            return null;
        }

        var sessionRequest = AbstractTypeFactory<ReedeemPunchoutSessionRequest>.TryCreateInstance();

        sessionRequest.SessionToken = sessionToken;

        return sessionRequest;
    }

    protected virtual async Task<AuthenticationTicket> CreateTicket(ClaimsPrincipal principal, TokenRequestContext context, PunchoutSession session)
    {
        // Explicitly no offline_access scope so no refresh_access is generated
        principal.SetScopes(
            Scopes.OpenId,
            Scopes.Email,
            Scopes.Profile,
            Scopes.Roles);

        principal.SetClaim("channelId", "punchout");
        principal.SetClaim("channelSessionId", session.Id);

        principal.SetResources("resource_server");

        // Claims are not included in the tokens unless they have a destination.
        principal.SetDestinations(claim => GetDestinations(claim, principal));

        // Set expire_in for the session lifetime
        principal.SetAccessTokenLifetime(session.ExpirationDate.Value - DateTime.UtcNow);

        // Create the authentication ticket
        var ticket = new AuthenticationTicket(principal, new AuthenticationProperties(), context.AuthenticationScheme);

        return ticket;
    }

    protected virtual IEnumerable<string> GetDestinations(Claim claim, ClaimsPrincipal principal)
    {
        // Same destinations as in the platform AuthorizationController.
        // Never include the security stamp in the access and identity tokens, as it's a secret value.
        if (claim.Type == _identityOptions.ClaimsIdentity.SecurityStampClaimType)
        {
            yield break;
        }

        yield return Destinations.AccessToken;

        if (claim.Type == Claims.Name && principal.HasScope(Scopes.Profile) ||
            claim.Type == Claims.Email && principal.HasScope(Scopes.Email) ||
            claim.Type == Claims.Role && principal.HasScope(Scopes.Roles))
        {
            yield return Destinations.IdentityToken;
        }
    }
}
