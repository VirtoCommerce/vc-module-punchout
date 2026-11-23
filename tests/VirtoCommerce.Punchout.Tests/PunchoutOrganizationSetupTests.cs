using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.Punchout.Core.Models;
using VirtoCommerce.Punchout.Core.Services;
using VirtoCommerce.Punchout.Data.Services;
using VirtoCommerce.Punchout.Tests.Fakes;
using Xunit;

namespace VirtoCommerce.Punchout.Tests;

[Trait("Category", "Unit")]
public class PunchoutOrganizationSetupTests
{
    private const string OrganizationId = "org-1";
    private const string OtherOrganizationId = "org-2";
    private const string StoreId = "store-1";
    private const string Email = "buyer@example.com";

    // User resolution

    [Fact]
    public async Task Resolve_OneUserOfOrganization_Found()
    {
        var user = CreateUser("user-1", "contact-1");
        var resolver = CreateResolver([user], CreateContact("contact-1", OrganizationId));

        var result = await resolver.ResolveAsync(CreateUserRequest());

        Assert.Equal(PunchoutOrganizationUserStatus.Found, result.Status);
        Assert.Equal("user-1", result.User.Id);
    }

    [Fact]
    public async Task Resolve_NoUserWithEmail_NotFound()
    {
        var resolver = CreateResolver([]);

        var result = await resolver.ResolveAsync(CreateUserRequest());

        Assert.Equal(PunchoutOrganizationUserStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task Resolve_SameEmailInAnotherOrganization_NotFound()
    {
        var user = CreateUser("user-1", "contact-1");
        var resolver = CreateResolver([user], CreateContact("contact-1", OtherOrganizationId));

        var result = await resolver.ResolveAsync(CreateUserRequest());

        Assert.Equal(PunchoutOrganizationUserStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task Resolve_SameEmailInTwoOrganizations_MatchesOnlyMappedOrganization()
    {
        var resolver = CreateResolver(
            [CreateUser("user-1", "contact-1"), CreateUser("user-2", "contact-2")],
            CreateContact("contact-1", OtherOrganizationId),
            CreateContact("contact-2", OrganizationId));

        var result = await resolver.ResolveAsync(CreateUserRequest());

        Assert.Equal(PunchoutOrganizationUserStatus.Found, result.Status);
        Assert.Equal("user-2", result.User.Id);
    }

    [Fact]
    public async Task Resolve_SeveralUsersOfOrganization_Ambiguous()
    {
        var resolver = CreateResolver(
            [CreateUser("user-1", "contact-1"), CreateUser("user-2", "contact-2")],
            CreateContact("contact-1", OrganizationId),
            CreateContact("contact-2", OrganizationId));

        var result = await resolver.ResolveAsync(CreateUserRequest());

        Assert.Equal(PunchoutOrganizationUserStatus.Ambiguous, result.Status);
        Assert.Equal(2, result.UserMatchCount);
        Assert.Null(result.User);
    }

    [Fact]
    public async Task Resolve_LockedOutUser_NotMatched()
    {
        var user = CreateUser("user-1", "contact-1");
        user.LockoutEnabled = true;
        user.LockoutEnd = DateTimeOffset.UtcNow.AddDays(1);
        var resolver = CreateResolver([user], CreateContact("contact-1", OrganizationId));

        var result = await resolver.ResolveAsync(CreateUserRequest());

        Assert.Equal(PunchoutOrganizationUserStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task Resolve_LockedOutUserNextToActiveUser_ActiveUserFound()
    {
        var lockedOut = CreateUser("user-1", "contact-1");
        lockedOut.LockoutEnabled = true;
        lockedOut.LockoutEnd = DateTimeOffset.UtcNow.AddDays(1);
        var resolver = CreateResolver(
            [lockedOut, CreateUser("user-2", "contact-2")],
            CreateContact("contact-1", OrganizationId),
            CreateContact("contact-2", OrganizationId));

        var result = await resolver.ResolveAsync(CreateUserRequest());

        Assert.Equal(PunchoutOrganizationUserStatus.Found, result.Status);
        Assert.Equal("user-2", result.User.Id);
    }

    [Theory]
    [InlineData(null, PunchoutOrganizationUserStatus.Found)]
    [InlineData(StoreId, PunchoutOrganizationUserStatus.Found)]
    [InlineData("other-store", PunchoutOrganizationUserStatus.NotFound)]
    public async Task Resolve_UserStore_MatchedWhenEmptyOrEqual(string userStoreId, string expected)
    {
        var user = CreateUser("user-1", "contact-1");
        user.StoreId = userStoreId;
        var resolver = CreateResolver([user], CreateContact("contact-1", OrganizationId));

        var result = await resolver.ResolveAsync(CreateUserRequest());

        Assert.Equal(expected, result.Status);
    }

    [Fact]
    public async Task Resolve_Administrator_NotMatched()
    {
        var user = CreateUser("user-1", "contact-1");
        user.IsAdministrator = true;
        var resolver = CreateResolver([user], CreateContact("contact-1", OrganizationId));

        var result = await resolver.ResolveAsync(CreateUserRequest());

        Assert.Equal(PunchoutOrganizationUserStatus.NotFound, result.Status);
    }

    // Setup

    [Fact]
    public void GetUserEmail_EndUserContact_ReturnsContactEmail()
    {
        var processor = new TestSetupProcessor(new FakeResolver(PunchoutOrganizationUserResult.NotFound()));
        var request = CreateRequest(contactEmail: " contact@example.com ", extrinsicEmail: "extrinsic@example.com");

        Assert.Equal("contact@example.com", processor.GetUserEmailPublic(request));
    }

    [Fact]
    public void GetUserEmail_ContactWithoutEmail_ReturnsExtrinsic()
    {
        var processor = new TestSetupProcessor(new FakeResolver(PunchoutOrganizationUserResult.NotFound()));
        var request = CreateRequest(contactEmail: null, extrinsicEmail: "extrinsic@example.com");

        Assert.Equal("extrinsic@example.com", processor.GetUserEmailPublic(request));
    }

    [Fact]
    public async Task ResolveOrganizationUser_NoEmail_InvalidRequest()
    {
        var resolver = new FakeResolver(PunchoutOrganizationUserResult.Found(CreateUser("user-1", "contact-1")));
        var processor = new TestSetupProcessor(resolver);

        var result = await processor.ResolveOrganizationUserPublicAsync(CreateRequest(null, null), CreateOrganizationMapping());

        Assert.Equal(PunchoutSetupStatus.InvalidRequest, result.Error?.Status);
        Assert.Null(resolver.Email);
    }

    [Fact]
    public async Task ResolveOrganizationUser_Found_ReturnsUser()
    {
        var resolver = new FakeResolver(PunchoutOrganizationUserResult.Found(CreateUser("user-1", "contact-1")));
        var processor = new TestSetupProcessor(resolver);

        var result = await processor.ResolveOrganizationUserPublicAsync(CreateRequest(Email, null), CreateOrganizationMapping());

        Assert.True(result.IsValid);
        Assert.Equal("user-1", result.UserId);
        Assert.Equal(OrganizationId, resolver.OrganizationId);
        Assert.Equal(StoreId, resolver.StoreId);
    }

    [Fact]
    public async Task ResolveOrganizationUser_NotFound_UserNotFound()
    {
        var processor = new TestSetupProcessor(new FakeResolver(PunchoutOrganizationUserResult.NotFound()));

        var result = await processor.ResolveOrganizationUserPublicAsync(CreateRequest(Email, null), CreateOrganizationMapping());

        Assert.Equal(PunchoutSetupStatus.UserNotFound, result.Error?.Status);
    }

    [Fact]
    public async Task ResolveOrganizationUser_Ambiguous_AmbiguousUser()
    {
        var processor = new TestSetupProcessor(new FakeResolver(PunchoutOrganizationUserResult.Ambiguous(2)));

        var result = await processor.ResolveOrganizationUserPublicAsync(CreateRequest(Email, null), CreateOrganizationMapping());

        Assert.Equal(PunchoutSetupStatus.AmbiguousUser, result.Error?.Status);
    }

    [Fact]
    public void CreateSession_OrganizationMapping_StoresMappingAndOrganization()
    {
        var processor = new TestSetupProcessor(new FakeResolver(PunchoutOrganizationUserResult.NotFound()));
        var mapping = CreateOrganizationMapping();
        var validationResult = PunchoutSetupValidationResult.Valid(CreateConfiguration(), mapping, "user-1", OrganizationId, null, "https://store.example.com");

        var session = processor.CreateSessionPublic(CreateRequest(Email, null), validationResult);

        Assert.Equal("user-1", session.UserId);
        Assert.Equal(mapping.Id, session.UserMappingId);
        Assert.Equal(OrganizationId, session.OrganizationId);
    }

    [Fact]
    public void CreateSession_UserMapping_NoOrganization()
    {
        var processor = new TestSetupProcessor(new FakeResolver(PunchoutOrganizationUserResult.NotFound()));
        var mapping = new PunchoutUserMapping { Id = "mapping-1", ExternalId = "sender", UserId = "user-1", MemberId = "contact-1" };
        var validationResult = PunchoutSetupValidationResult.Valid(CreateConfiguration(), mapping, mapping.UserId, null, null, "https://store.example.com");

        var session = processor.CreateSessionPublic(CreateRequest(Email, null), validationResult);

        Assert.Equal("user-1", session.UserId);
        Assert.Equal("mapping-1", session.UserMappingId);
        Assert.Null(session.OrganizationId);
    }

    private static PunchoutOrganizationUserRequest CreateUserRequest() => new()
    {
        OrganizationId = OrganizationId,
        Email = Email,
        StoreId = StoreId,
    };

    private static PunchoutOrganizationUserResolver CreateResolver(IList<ApplicationUser> users, params Member[] members)
    {
        return new TestResolver(users, MemberServiceProxy.Create(members));
    }

    private static ApplicationUser CreateUser(string id, string memberId) => new()
    {
        Id = id,
        UserName = id,
        Email = Email,
        MemberId = memberId,
        UserType = nameof(UserType.Customer),
    };

    private static Contact CreateContact(string id, string organizationId) => new()
    {
        Id = id,
        Organizations = [organizationId],
    };

    private static PunchoutUserMapping CreateOrganizationMapping() => new()
    {
        Id = "mapping-1",
        IsActive = true,
        ExternalId = "sender",
        OrganizationId = OrganizationId,
    };

    private static PunchoutConfiguration CreateConfiguration() => new() { Id = "config-1", StoreId = StoreId, SharedSecret = "secret" };

    private static PunchoutSetupRequest CreateRequest(string contactEmail, string extrinsicEmail) => new()
    {
        Sender = "sender",
        User = contactEmail is null ? null : new PunchoutUserContext { Name = "Buyer", Email = contactEmail },
        Extrinsics = extrinsicEmail is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["UserEmail"] = extrinsicEmail },
    };

    private sealed class TestResolver(IList<ApplicationUser> users, IMemberService memberService)
        : PunchoutOrganizationUserResolver(CreateUserManager(), memberService, new PunchoutUserPolicy())
    {
        // The real lookup queries the database
        protected override Task<IList<ApplicationUser>> FindUsersByEmailAsync(string email) =>
            Task.FromResult<IList<ApplicationUser>>(users.Where(x => x.Email.Equals(email, StringComparison.OrdinalIgnoreCase)).ToList());

        private static UserManager<ApplicationUser> CreateUserManager() =>
            new(DispatchProxy.Create<IUserLockoutStore<ApplicationUser>, LockoutStoreProxy>(),
                null, null, null, null, new UpperInvariantLookupNormalizer(), null, null, NullLogger<UserManager<ApplicationUser>>.Instance);
    }

    private sealed class TestSetupProcessor(IPunchoutOrganizationUserResolver resolver)
        : PunchoutSetupProcessor(
            Microsoft.Extensions.Options.Options.Create(new PunchoutOptions { Configurations = [CreateConfiguration()] }),
            null, null, null, null, resolver, NullLogger<PunchoutSetupProcessor>.Instance)
    {
        public string GetUserEmailPublic(PunchoutSetupRequest request) => GetUserEmail(request);

        public Task<PunchoutSetupValidationResult> ResolveOrganizationUserPublicAsync(PunchoutSetupRequest request, PunchoutUserMapping mapping) =>
            ResolveOrganizationUserAsync(request, mapping, CreateConfiguration());

        public PunchoutSession CreateSessionPublic(PunchoutSetupRequest request, PunchoutSetupValidationResult validationResult) =>
            CreateSession(request, validationResult, "token");
    }

    private sealed class FakeResolver(PunchoutOrganizationUserResult result) : IPunchoutOrganizationUserResolver
    {
        public string OrganizationId { get; private set; }

        public string Email { get; private set; }

        public string StoreId { get; private set; }

        public Task<PunchoutOrganizationUserResult> ResolveAsync(PunchoutOrganizationUserRequest request)
        {
            OrganizationId = request.OrganizationId;
            Email = request.Email;
            StoreId = request.StoreId;

            return Task.FromResult(result);
        }
    }

    /// <summary>
    /// The lockout part of the user store, read from the user.
    /// </summary>
    public class LockoutStoreProxy : DispatchProxy
    {
        protected override object Invoke(MethodInfo targetMethod, object[] args)
        {
            return targetMethod?.Name switch
            {
                nameof(IUserLockoutStore<ApplicationUser>.GetLockoutEnabledAsync) => Task.FromResult(((ApplicationUser)args[0]).LockoutEnabled),
                nameof(IUserLockoutStore<ApplicationUser>.GetLockoutEndDateAsync) => Task.FromResult(((ApplicationUser)args[0]).LockoutEnd),
                nameof(IDisposable.Dispose) => null,
                _ => throw new NotSupportedException(targetMethod?.Name),
            };
        }
    }
}
