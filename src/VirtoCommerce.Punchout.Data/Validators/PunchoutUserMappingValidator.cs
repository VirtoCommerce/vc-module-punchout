using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Identity;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.Punchout.Core.Models;
using VirtoCommerce.Punchout.Core.Services;

namespace VirtoCommerce.Punchout.Data.Validators;

public class PunchoutUserMappingValidator : AbstractValidator<PunchoutUserMapping>
{
    private readonly IPunchoutUserMappingSearchService _searchService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IMemberService _memberService;
    private readonly IPunchoutUserPolicy _userPolicy;

    public PunchoutUserMappingValidator(
        IPunchoutUserMappingSearchService searchService,
        UserManager<ApplicationUser> userManager,
        IMemberService memberService,
        IPunchoutUserPolicy userPolicy)
    {
        _searchService = searchService;
        _userManager = userManager;
        _memberService = memberService;
        _userPolicy = userPolicy;

        ClassLevelCascadeMode = CascadeMode.Stop;
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.ExternalId)
            .NotEmpty()
            .WithErrorCode("externalId-required");

        // Either a user or an organization mapping
        RuleFor(x => x)
            .Must(x => x.UserId.IsNullOrEmpty() != x.OrganizationId.IsNullOrEmpty())
            .WithErrorCode("userId-or-organizationId-required");

        RuleFor(x => x.ExternalId)
            .MustAsync(BeUniqueExternalIdAsync)
            .WithErrorCode("mapping-exists")
            .WithState(x => new { externalId = x.ExternalId });

        When(x => !x.IsOrganizationMapping, () =>
        {
            RuleFor(x => x)
                .Must(x => !x.MemberId.IsNullOrEmpty())
                .WithErrorCode("userId-memberId-required");

            RuleFor(x => x)
                .CustomAsync(ValidateUserAsync);
        });

        When(x => x.IsOrganizationMapping, () =>
        {
            RuleFor(x => x)
                .CustomAsync(ValidateOrganizationAsync);

            // The requisitions of an organization are shared by its mapping, and the admin shows one mapping per organization
            RuleFor(x => x.OrganizationId)
                .MustAsync(BeSingleOrganizationMappingAsync)
                .WithErrorCode("organization-mapping-exists");
        });
    }

    /// <summary>
    /// An organization can have one organization mapping.
    /// </summary>
    private async Task<bool> BeSingleOrganizationMappingAsync(PunchoutUserMapping model, string organizationId, CancellationToken cancellationToken)
    {
        var criteria = AbstractTypeFactory<PunchoutUserMappingSearchCriteria>.TryCreateInstance();
        criteria.OrganizationIds = [organizationId];

        var existing = await _searchService.SearchNoCloneAsync(criteria);

        return existing.Results.All(x => x.Id == model.Id || !x.IsOrganizationMapping);
    }

    /// <summary>
    /// An identity can have one user mapping and one organization mapping.
    /// </summary>
    private async Task<bool> BeUniqueExternalIdAsync(PunchoutUserMapping model, string externalId, CancellationToken cancellationToken)
    {
        var criteria = AbstractTypeFactory<PunchoutUserMappingSearchCriteria>.TryCreateInstance();
        criteria.ExternalIds = [externalId];

        var existing = await _searchService.SearchNoCloneAsync(criteria);

        return existing.Results.All(x => x.Id == model.Id || x.IsOrganizationMapping != model.IsOrganizationMapping);
    }

    private async Task ValidateUserAsync(PunchoutUserMapping model, ValidationContext<PunchoutUserMapping> context, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(model.UserId);
        if (user == null || user.MemberId != model.MemberId)
        {
            context.AddFailure(new ValidationFailure(nameof(model.MemberId), null) { ErrorCode = "memberId-mismatch" });
            return;
        }

        if (!_userPolicy.IsAllowed(user))
        {
            context.AddFailure(new ValidationFailure(nameof(model.UserId), null) { ErrorCode = "user-is-admin-or-customer" });
        }
    }

    private async Task ValidateOrganizationAsync(PunchoutUserMapping model, ValidationContext<PunchoutUserMapping> context, CancellationToken cancellationToken)
    {
        var member = await _memberService.GetByIdAsync(model.OrganizationId, nameof(MemberResponseGroup.Default), nameof(Organization));
        if (member is not Organization)
        {
            context.AddFailure(new ValidationFailure(nameof(model.OrganizationId), null)
            {
                ErrorCode = "organization-not-found",
                CustomState = new { organizationId = model.OrganizationId },
            });
        }
    }
}
