using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VirtoCommerce.Platform.Data.Infrastructure;
using VirtoCommerce.Punchout.Core.Services;
using VirtoCommerce.Punchout.Data.Repositories;

namespace VirtoCommerce.Punchout.Data.Services;

public class PunchoutOrganizationIntegrationService(
    Func<IPunchoutRepository> repositoryFactory,
    IPunchoutIntegrationService crudService)
    : IPunchoutOrganizationIntegrationService
{
    public virtual async Task<IList<string>> GetIntegrationIdsAsync(string organizationId)
    {
        ArgumentException.ThrowIfNullOrEmpty(organizationId);

        using var repository = repositoryFactory();
        repository.DisableChangesTracking();

        return await repository.PunchoutIntegrations
            .Where(x => x.OrganizationId == organizationId)
            .Select(x => x.Id)
            .ToListAsync();
    }

    public virtual async Task SetIntegrationsAsync(string organizationId, IList<string> integrationIds)
    {
        ArgumentException.ThrowIfNullOrEmpty(organizationId);

        var requestedIds = integrationIds?
            .Where(x => !string.IsNullOrEmpty(x))
            .Distinct()
            .ToList() ?? [];

        var currentIds = await GetIntegrationIdsAsync(organizationId);

        // Saving only the integrations that actually change keeps concurrent edits of other
        // integrations from being overwritten, and keeps their caches warm.
        var addedIds = requestedIds.Except(currentIds).ToList();
        var removedIds = currentIds.Except(requestedIds).ToList();

        if (addedIds.Count == 0 && removedIds.Count == 0)
        {
            return;
        }

        var integrations = await crudService.GetAsync([.. addedIds, .. removedIds]);

        // An integration belongs to exactly one organization, so taking over one that is already
        // assigned elsewhere would silently move it away from an organization the caller cannot see.
        var conflictingIds = integrations
            .Where(x => addedIds.Contains(x.Id) &&
                        !string.IsNullOrEmpty(x.OrganizationId) &&
                        x.OrganizationId != organizationId)
            .Select(x => x.Id)
            .ToList();

        if (conflictingIds.Count > 0)
        {
            throw new InvalidOperationException(
                $"Punchout integrations {string.Join(", ", conflictingIds)} are already assigned to another organization.");
        }

        foreach (var integration in integrations)
        {
            integration.OrganizationId = requestedIds.Contains(integration.Id) ? organizationId : null;
        }

        await crudService.SaveChangesAsync(integrations);
    }
}
