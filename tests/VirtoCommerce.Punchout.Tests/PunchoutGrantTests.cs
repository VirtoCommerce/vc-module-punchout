using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using OpenIddict.Abstractions;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.Platform.Core.Security.SignInLog;
using VirtoCommerce.Platform.Security.OpenIddict;
using VirtoCommerce.Punchout.Core;
using VirtoCommerce.Punchout.Core.Models;
using VirtoCommerce.Punchout.Core.Services;
using VirtoCommerce.Punchout.Data.Services;
using Xunit;

namespace VirtoCommerce.Punchout.Tests;

[Trait("Category", "Unit")]
public class PunchoutGrantTests
{
    private const string OrganizationId = "org-1";

    private static readonly ApplicationUser User = new() { Id = "user-1", MemberId = "contact-1", UserType = nameof(UserType.Customer) };

    // Mapping check

    [Fact]
    public async Task HasActiveMapping_ActiveUserMapping_True()
    {
        var handler = CreateHandler(CreateUserMapping());

        Assert.True(await handler.HasActiveUserMappingPublicAsync(CreateSession(), User));
    }

    [Fact]
    public async Task HasActiveMapping_DeactivatedAfterSetup_False()
    {
        var mapping = CreateUserMapping();
        mapping.IsActive = false;
        var handler = CreateHandler(mapping);

        Assert.False(await handler.HasActiveUserMappingPublicAsync(CreateSession(), User));
    }

    [Fact]
    public async Task HasActiveMapping_MappingDeleted_False()
    {
        var handler = CreateHandler();

        Assert.False(await handler.HasActiveUserMappingPublicAsync(CreateSession(), User));
    }

    [Fact]
    public async Task HasActiveMapping_NoMappingIdInSession_False()
    {
        var handler = CreateHandler(CreateUserMapping());
        var session = CreateSession();
        session.UserMappingId = null;

        Assert.False(await handler.HasActiveUserMappingPublicAsync(session, User));
    }

    [Fact]
    public async Task HasActiveMapping_UserMappingRelinkedToAnotherUser_False()
    {
        var mapping = CreateUserMapping();
        mapping.UserId = "user-2";
        var handler = CreateHandler(mapping);

        Assert.False(await handler.HasActiveUserMappingPublicAsync(CreateSession(), User));
    }

    [Fact]
    public async Task HasActiveMapping_OrganizationUser_True()
    {
        var handler = CreateHandler(CreateOrganizationMapping(), isOrganizationUser: true);

        Assert.True(await handler.HasActiveUserMappingPublicAsync(CreateSession(OrganizationId), User));
    }

    [Fact]
    public async Task HasActiveMapping_UserRemovedFromOrganization_False()
    {
        var handler = CreateHandler(CreateOrganizationMapping(), isOrganizationUser: false);

        Assert.False(await handler.HasActiveUserMappingPublicAsync(CreateSession(OrganizationId), User));
    }

    [Fact]
    public async Task HasActiveMapping_OrganizationMappingDeactivated_False()
    {
        var mapping = CreateOrganizationMapping();
        mapping.IsActive = false;
        var handler = CreateHandler(mapping, isOrganizationUser: true);

        Assert.False(await handler.HasActiveUserMappingPublicAsync(CreateSession(OrganizationId), User));
    }

    [Fact]
    public async Task HasActiveMapping_OrganizationMappingRelinkedToAnotherOrganization_False()
    {
        var mapping = CreateOrganizationMapping();
        mapping.OrganizationId = "org-2";
        var handler = CreateHandler(mapping, isOrganizationUser: true);

        Assert.False(await handler.HasActiveUserMappingPublicAsync(CreateSession(OrganizationId), User));
    }

    // Organization of the session

    [Fact]
    public async Task OrganizationValidator_SessionOrganizationKept_Succeeds()
    {
        var context = CreateTokenRequestContext(sessionOrganizationId: OrganizationId, requestedOrganizationId: OrganizationId);

        var errors = await new PunchoutOrganizationRequestValidator().ValidateAsync(context);

        Assert.Empty(errors);
    }

    [Fact]
    public async Task OrganizationValidator_SwitchedToAnotherOrganization_Fails()
    {
        // The Customer module's validator replaces a locked organization with another one of the user
        var context = CreateTokenRequestContext(sessionOrganizationId: OrganizationId, requestedOrganizationId: "org-2");

        var errors = await new PunchoutOrganizationRequestValidator().ValidateAsync(context);

        Assert.Single(errors);
        Assert.Equal(SignInFailureReason.NotAllowed, context.FailureReason);
    }

    [Fact]
    public async Task OrganizationValidator_OrganizationCleared_Fails()
    {
        var context = CreateTokenRequestContext(sessionOrganizationId: OrganizationId, requestedOrganizationId: null);

        var errors = await new PunchoutOrganizationRequestValidator().ValidateAsync(context);

        Assert.Single(errors);
    }

    [Fact]
    public async Task OrganizationValidator_UserMappingSession_Skipped()
    {
        var context = CreateTokenRequestContext(sessionOrganizationId: null, requestedOrganizationId: "org-2");

        var errors = await new PunchoutOrganizationRequestValidator().ValidateAsync(context);

        Assert.Empty(errors);
    }

    [Fact]
    public async Task OrganizationValidator_OtherGrant_Skipped()
    {
        var context = CreateTokenRequestContext(sessionOrganizationId: OrganizationId, requestedOrganizationId: "org-2");
        context.Request.GrantType = "password";

        var errors = await new PunchoutOrganizationRequestValidator().ValidateAsync(context);

        Assert.Empty(errors);
    }

    private static TokenRequestContext CreateTokenRequestContext(string sessionOrganizationId, string requestedOrganizationId)
    {
        var request = new OpenIddictRequest { GrantType = ModuleConstants.Security.PunchoutGrantType };

        if (requestedOrganizationId != null)
        {
            request.SetParameter(ModuleConstants.Security.Parameters.OrganizationId, requestedOrganizationId);
        }

        var context = new TokenRequestContext { Request = request };

        if (sessionOrganizationId != null)
        {
            context.AdditionalParameters.Add(ModuleConstants.Security.Parameters.SessionOrganizationId, sessionOrganizationId);
        }

        return context;
    }

    private static TestGrantHandler CreateHandler(PunchoutUserMapping mapping = null, bool isOrganizationUser = false)
    {
        var mappingService = DispatchProxy.Create<IPunchoutUserMappingService, UserMappingServiceProxy>();
        ((UserMappingServiceProxy)(object)mappingService).Mapping = mapping;

        return new TestGrantHandler(mappingService, new FakeOrganizationUserResolver(isOrganizationUser));
    }

    private static PunchoutSession CreateSession(string organizationId = null) => new()
    {
        Id = "session-1",
        UserId = User.Id,
        UserMappingId = "mapping-1",
        OrganizationId = organizationId,
    };

    private static PunchoutUserMapping CreateUserMapping() => new()
    {
        Id = "mapping-1",
        IsActive = true,
        ExternalId = "sender",
        UserId = User.Id,
        MemberId = User.MemberId,
    };

    private static PunchoutUserMapping CreateOrganizationMapping() => new()
    {
        Id = "mapping-1",
        IsActive = true,
        ExternalId = "sender",
        OrganizationId = OrganizationId,
    };

    private sealed class TestGrantHandler(IPunchoutUserMappingService mappingService, IPunchoutOrganizationUserResolver resolver)
        : PunchoutGrantTypeHandler(
            null,
            Microsoft.Extensions.Options.Options.Create(new IdentityOptions()),
            Array.Empty<ITokenRequestValidator>(),
            Array.Empty<ITokenClaimProvider>(),
            Array.Empty<ITokenRequestHandler>(),
            null,
            null,
            null,
            mappingService,
            resolver,
            new PunchoutUserPolicy())
    {
        public Task<bool> HasActiveUserMappingPublicAsync(PunchoutSession session, ApplicationUser user) =>
            HasActiveUserMappingAsync(session, user);
    }

    private sealed class FakeOrganizationUserResolver(bool isOrganizationUser) : IPunchoutOrganizationUserResolver
    {
        public Task<PunchoutOrganizationUserResult> ResolveAsync(PunchoutOrganizationUserRequest request) =>
            throw new NotSupportedException();

        public Task<bool> IsOrganizationUserAsync(ApplicationUser user, string organizationId) =>
            Task.FromResult(isOrganizationUser);
    }

    /// <summary>
    /// In-memory mapping service supporting GetAsync.
    /// </summary>
    public class UserMappingServiceProxy : DispatchProxy
    {
        public PunchoutUserMapping Mapping { get; set; }

        protected override object Invoke(MethodInfo targetMethod, object[] args)
        {
            if (targetMethod?.Name == nameof(IPunchoutUserMappingService.GetAsync))
            {
                var ids = (IList<string>)args[0];
                IList<PunchoutUserMapping> result = Mapping != null && ids.Contains(Mapping.Id) ? [Mapping] : [];
                return Task.FromResult(result);
            }

            throw new NotSupportedException(targetMethod?.Name);
        }
    }
}
