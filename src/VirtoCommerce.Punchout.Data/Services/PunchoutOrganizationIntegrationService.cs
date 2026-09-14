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

        return await repository.PunchoutIntegrationOrganizations
            .Where(x => x.OrganizationId == organizationId)
            .Select(x => x.IntegrationId)
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

        // Saving only the integrations whose links actually change keeps concurrent edits of other
        // integrations from being overwritten, and keeps their caches warm.
        var changedIds = requestedIds.Except(currentIds)
            .Concat(currentIds.Except(requestedIds))
            .ToList();

        if (changedIds.Count == 0)
        {
            return;
        }

        var integrations = await crudService.GetAsync(changedIds);

        foreach (var integration in integrations)
        {
            integration.OrganizationIds ??= [];

            if (requestedIds.Contains(integration.Id))
            {
                if (!integration.OrganizationIds.Contains(organizationId))
                {
                    integration.OrganizationIds.Add(organizationId);
                }
            }
            else
            {
                integration.OrganizationIds.Remove(organizationId);
            }
        }

        await crudService.SaveChangesAsync(integrations);
    }
}
