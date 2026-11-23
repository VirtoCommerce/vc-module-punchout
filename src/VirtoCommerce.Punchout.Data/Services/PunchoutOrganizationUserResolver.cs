using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.Punchout.Core.Models;
using VirtoCommerce.Punchout.Core.Services;

namespace VirtoCommerce.Punchout.Data.Services;

public class PunchoutOrganizationUserResolver(
    UserManager<ApplicationUser> userManager,
    IMemberService memberService,
    IPunchoutUserPolicy userPolicy)
    : IPunchoutOrganizationUserResolver
{
    public virtual async Task<PunchoutOrganizationUserResult> ResolveAsync(PunchoutOrganizationUserRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.OrganizationId.IsNullOrEmpty() || request.Email.IsNullOrWhiteSpace())
        {
            return PunchoutOrganizationUserResult.NotFound();
        }

        var users = await FindUsersByEmailAsync(request.Email.Trim());
        var candidates = new List<ApplicationUser>();

        foreach (var user in users)
        {
            if (await IsCandidateAsync(user, request))
            {
                candidates.Add(user);
            }
        }

        var matches = await FilterOrganizationUsersAsync(candidates, request.OrganizationId);

        return matches.Count switch
        {
            0 => PunchoutOrganizationUserResult.NotFound(),
            1 => PunchoutOrganizationUserResult.Found(matches[0]),
            _ => PunchoutOrganizationUserResult.Ambiguous(matches.Count),
        };
    }

    protected virtual async Task<IList<ApplicationUser>> FindUsersByEmailAsync(string email)
    {
        // FindByEmailAsync throws when several users share the email
        var normalizedEmail = userManager.NormalizeEmail(email);

        var userIds = await userManager.Users
            .Where(x => x.NormalizedEmail == normalizedEmail)
            .Select(x => x.Id)
            .ToListAsync();

        var result = new List<ApplicationUser>();

        foreach (var userId in userIds)
        {
            var user = await userManager.FindByIdAsync(userId);
            if (user != null)
            {
                result.Add(user);
            }
        }

        return result;
    }

    protected virtual async Task<bool> IsCandidateAsync(ApplicationUser user, PunchoutOrganizationUserRequest request)
    {
        if (user.MemberId.IsNullOrEmpty() || !userPolicy.IsAllowed(user))
        {
            return false;
        }

        if (!user.StoreId.IsNullOrEmpty() && !user.StoreId.EqualsIgnoreCase(request.StoreId))
        {
            return false;
        }

        return !await userManager.IsLockedOutAsync(user);
    }

    protected virtual async Task<IList<ApplicationUser>> FilterOrganizationUsersAsync(IList<ApplicationUser> users, string organizationId)
    {
        if (users.Count == 0)
        {
            return [];
        }

        var memberIds = users.Select(x => x.MemberId).Distinct().ToArray();
        var members = await memberService.GetByIdsAsync(memberIds);

        var organizationMemberIds = members
            .OfType<Contact>()
            .Where(x => x.Organizations?.Contains(organizationId) == true)
            .Select(x => x.Id)
            .ToHashSet();

        return users.Where(x => organizationMemberIds.Contains(x.MemberId)).ToList();
    }
}
