using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Identity;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.Punchout.Core.Models;
using VirtoCommerce.Punchout.Core.Services;

namespace VirtoCommerce.Punchout.Data.Validators;

public class PunchoutUserMappingValidator : AbstractValidator<PunchoutUserMapping>
{
    private readonly IPunchoutUserMappingSearchService _searchService;
    private readonly UserManager<ApplicationUser> _userManager;

    public PunchoutUserMappingValidator(
        IPunchoutUserMappingSearchService searchService,
        UserManager<ApplicationUser> userManager)
    {
        _searchService = searchService;
        _userManager = userManager;

        ClassLevelCascadeMode = CascadeMode.Stop;
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.ExternalId)
            .NotEmpty()
            .WithErrorCode("externalId-required")
            .MustAsync(BeUniqueExternalIdAsync)
            .WithErrorCode("mapping-exists")
            .WithState(x => new { externalId = x.ExternalId });

        RuleFor(x => x)
            .Must(x => !x.UserId.IsNullOrEmpty() && !x.MemberId.IsNullOrEmpty())
            .WithErrorCode("userId-memberId-required");

        RuleFor(x => x)
            .CustomAsync(ValidateUserAsync);
    }

    private async Task<bool> BeUniqueExternalIdAsync(PunchoutUserMapping model, string externalId, CancellationToken cancellationToken)
    {
        var criteria = AbstractTypeFactory<PunchoutUserMappingSearchCriteria>.TryCreateInstance();
        criteria.ExternalIds = [externalId];
        criteria.Take = 1;

        var existing = await _searchService.SearchNoCloneAsync(criteria);

        return existing.Results.All(x => x.Id == model.Id);
    }

    private async Task ValidateUserAsync(PunchoutUserMapping model, ValidationContext<PunchoutUserMapping> context, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(model.UserId);
        if (user == null || user.MemberId != model.MemberId)
        {
            context.AddFailure(new ValidationFailure(nameof(model.MemberId), null) { ErrorCode = "memberId-mismatch" });
            return;
        }

        if (user.IsAdministrator || user.UserType != nameof(UserType.Customer))
        {
            context.AddFailure(new ValidationFailure(nameof(model.UserId), null) { ErrorCode = "user-is-admin-or-customer" });
        }
    }
}
