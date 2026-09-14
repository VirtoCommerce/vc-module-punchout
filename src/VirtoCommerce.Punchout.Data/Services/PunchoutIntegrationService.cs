using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.Platform.Core.Caching;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Events;
using VirtoCommerce.Platform.Data.GenericCrud;
using VirtoCommerce.Punchout.Core.Events;
using VirtoCommerce.Punchout.Core.Models;
using VirtoCommerce.Punchout.Core.Services;
using VirtoCommerce.Punchout.Data.Models;
using VirtoCommerce.Punchout.Data.Repositories;

namespace VirtoCommerce.Punchout.Data.Services;

public class PunchoutIntegrationService(
    Func<IPunchoutRepository> repositoryFactory,
    IPlatformMemoryCache platformMemoryCache,
    IEventPublisher eventPublisher,
    IPunchoutSecretHasher secretHasher)
    : CrudService<PunchoutIntegration, PunchoutIntegrationEntity, PunchoutIntegrationChangingEvent, PunchoutIntegrationChangedEvent>
        (repositoryFactory, platformMemoryCache, eventPublisher),
        IPunchoutIntegrationService
{
    protected override Task<IList<PunchoutIntegrationEntity>> LoadEntities(IRepository repository, IList<string> ids, string responseGroup)
    {
        return ((IPunchoutRepository)repository).GetPunchoutIntegrationsByIdsAsync(ids, responseGroup);
    }

    /// <summary>
    /// Replaces the plain shared secret with its hash, so that the secret itself never reaches the database.
    /// A model without a shared secret keeps the hash already stored for it.
    /// </summary>
    protected override Task BeforeSaveChanges(IList<PunchoutIntegration> models)
    {
        foreach (var model in models)
        {
            if (!string.IsNullOrEmpty(model.SharedSecret))
            {
                model.SharedSecretHash = secretHasher.HashSecret(model.SharedSecret);
                model.SharedSecret = null;
            }
        }

        return base.BeforeSaveChanges(models);
    }
}
