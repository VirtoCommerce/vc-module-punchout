using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.Punchout.Core.Models;
using VirtoCommerce.Punchout.Core.Services;
using VirtoCommerce.Punchout.Data.Services;
using VirtoCommerce.Punchout.Data.Validators;
using VirtoCommerce.Punchout.Tests.Fakes;
using Xunit;

namespace VirtoCommerce.Punchout.Tests;

[Trait("Category", "Unit")]
public class PunchoutUserMappingTests
{
    private const string Identity = "coupa-sender";
    private const string OrganizationId = "org-1";

    // Validation

    [Fact]
    public async Task Validate_NeitherUserNorOrganization_Fails()
    {
        var validator = CreateValidator([]);

        var result = await validator.ValidateAsync(new PunchoutUserMapping { ExternalId = Identity }, TestContext.Current.CancellationToken);

        Assert.Equal("userId-or-organizationId-required", Assert.Single(result.Errors).ErrorCode);
    }

    [Fact]
    public async Task Validate_BothUserAndOrganization_Fails()
    {
        var validator = CreateValidator([]);

        var result = await validator.ValidateAsync(new PunchoutUserMapping
        {
            ExternalId = Identity,
            UserId = "user-1",
            MemberId = "contact-1",
            OrganizationId = OrganizationId,
        }, TestContext.Current.CancellationToken);

        Assert.Equal("userId-or-organizationId-required", Assert.Single(result.Errors).ErrorCode);
    }

    [Fact]
    public async Task Validate_OrganizationMapping_Succeeds()
    {
        var validator = CreateValidator([]);

        var result = await validator.ValidateAsync(CreateOrganizationMapping(), TestContext.Current.CancellationToken);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validate_OrganizationNotFound_Fails()
    {
        var validator = CreateValidator([]);
        var mapping = CreateOrganizationMapping();
        mapping.OrganizationId = "unknown";

        var result = await validator.ValidateAsync(mapping, TestContext.Current.CancellationToken);

        Assert.Equal("organization-not-found", Assert.Single(result.Errors).ErrorCode);
    }

    [Fact]
    public async Task Validate_OrganizationMappingWithMember_Fails()
    {
        var validator = CreateValidator([]);
        var mapping = CreateOrganizationMapping();
        mapping.MemberId = "contact-1";

        var result = await validator.ValidateAsync(mapping, TestContext.Current.CancellationToken);

        Assert.Equal("organization-memberId-not-allowed", Assert.Single(result.Errors).ErrorCode);
    }

    [Fact]
    public async Task Validate_OrganizationMappingNextToUserMapping_Succeeds()
    {
        var validator = CreateValidator([CreateUserMapping("existing")]);

        var result = await validator.ValidateAsync(CreateOrganizationMapping(), TestContext.Current.CancellationToken);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validate_SecondOrganizationMappingForIdentity_Fails()
    {
        var existing = CreateOrganizationMapping("existing");
        var validator = CreateValidator([existing]);

        var result = await validator.ValidateAsync(CreateOrganizationMapping(), TestContext.Current.CancellationToken);

        Assert.Equal("mapping-exists", Assert.Single(result.Errors).ErrorCode);
    }

    [Fact]
    public async Task Validate_SecondMappingForOrganization_Fails()
    {
        // Two identities of one organization would share its requisitions, and the admin shows one mapping per organization
        var existing = CreateOrganizationMapping("existing");
        var validator = CreateValidator([existing]);

        var mapping = CreateOrganizationMapping();
        mapping.ExternalId = "another-sender";

        var result = await validator.ValidateAsync(mapping, TestContext.Current.CancellationToken);

        Assert.Equal("organization-mapping-exists", Assert.Single(result.Errors).ErrorCode);
    }

    [Fact]
    public async Task Validate_UpdateOfSameOrganizationMapping_Succeeds()
    {
        var existing = CreateOrganizationMapping("existing");
        var validator = CreateValidator([existing]);

        var result = await validator.ValidateAsync(CreateOrganizationMapping("existing"), TestContext.Current.CancellationToken);

        Assert.True(result.IsValid);
    }

    // Lookup

    [Fact]
    public async Task FindUserMapping_UserAndOrganizationMappings_ReturnsUserMapping()
    {
        // The organization mapping is newer, so the search returns it first
        var processor = new TestProcessor([CreateOrganizationMapping("org-mapping"), CreateUserMapping("user-mapping")]);

        var mapping = await processor.FindUserMappingPublicAsync(Identity);

        Assert.Equal("user-mapping", mapping.Id);
    }

    [Fact]
    public async Task FindUserMapping_OnlyOrganizationMapping_ReturnsOrganizationMapping()
    {
        var processor = new TestProcessor([CreateOrganizationMapping("org-mapping")]);

        var mapping = await processor.FindUserMappingPublicAsync(Identity);

        Assert.Equal("org-mapping", mapping.Id);
        Assert.True(mapping.IsOrganizationMapping);
    }

    [Fact]
    public async Task FindUserMapping_InactiveUserMapping_ReturnsOrganizationMapping()
    {
        var userMapping = CreateUserMapping("user-mapping");
        userMapping.IsActive = false;
        var processor = new TestProcessor([userMapping, CreateOrganizationMapping("org-mapping")]);

        var mapping = await processor.FindUserMappingPublicAsync(Identity);

        Assert.Equal("org-mapping", mapping.Id);
    }

    [Fact]
    public void GetMappingIdentity_ReturnsSender()
    {
        var processor = new TestProcessor([]);

        Assert.Equal(Identity, processor.GetMappingIdentityPublic(new PunchoutSetupRequest { Sender = Identity, From = "buyer" }));
    }

    private static PunchoutUserMappingValidator CreateValidator(IList<PunchoutUserMapping> mappings)
    {
        var memberService = MemberServiceProxy.Create(new Organization { Id = OrganizationId, Name = "Organization" });

        // The user manager is used for user mappings only
        return new PunchoutUserMappingValidator(new InMemoryUserMappingSearchService(mappings), null, memberService, new PunchoutUserPolicy());
    }

    private static PunchoutUserMapping CreateOrganizationMapping(string id = null) => new()
    {
        Id = id,
        IsActive = true,
        ExternalId = Identity,
        OrganizationId = OrganizationId,
        OrganizationName = "Organization",
    };

    private static PunchoutUserMapping CreateUserMapping(string id = null) => new()
    {
        Id = id,
        IsActive = true,
        ExternalId = Identity,
        UserId = "user-1",
        MemberId = "contact-1",
    };

    private sealed class TestProcessor(IList<PunchoutUserMapping> mappings)
        : BasePunchoutProcessor(Microsoft.Extensions.Options.Options.Create(new PunchoutOptions()), new InMemoryUserMappingSearchService(mappings))
    {
        public Task<PunchoutUserMapping> FindUserMappingPublicAsync(string identity) => FindUserMappingAsync(identity);

        public string GetMappingIdentityPublic(BasePunchoutRequest request) => GetMappingIdentity(request);
    }

    private sealed class InMemoryUserMappingSearchService(IList<PunchoutUserMapping> mappings) : IPunchoutUserMappingSearchService
    {
        public Task<PunchoutUserMappingSearchResult> SearchAsync(PunchoutUserMappingSearchCriteria criteria, bool clone = true)
        {
            var results = mappings
                .Where(x => criteria.ExternalIds == null || criteria.ExternalIds.Contains(x.ExternalId))
                .Where(x => criteria.OrganizationIds == null || criteria.OrganizationIds.Contains(x.OrganizationId))
                .Where(x => criteria.IsActive == null || x.IsActive == criteria.IsActive)
                .ToList();

            return Task.FromResult(new PunchoutUserMappingSearchResult { Results = results, TotalCount = results.Count });
        }
    }
}
