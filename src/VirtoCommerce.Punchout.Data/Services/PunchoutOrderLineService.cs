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

/// <summary>
/// The ordered items decide which purchase order may order a requisition item, they are always read from the database.
/// </summary>
public class PunchoutOrderLineService(
    Func<IPunchoutRepository> repositoryFactory,
    IPlatformMemoryCache platformMemoryCache,
    IEventPublisher eventPublisher)
    : CrudService<PunchoutOrderLine, PunchoutOrderLineEntity, PunchoutOrderLineChangingEvent, PunchoutOrderLineChangedEvent>
        (repositoryFactory, platformMemoryCache, eventPublisher),
        IPunchoutOrderLineService
{
    public override async Task<IList<PunchoutOrderLine>> GetAsync(IList<string> ids, string responseGroup = null, bool clone = true)
    {
        using (CacheDisabler.DisableCache())
        {
            return await base.GetAsync(ids, responseGroup, clone);
        }
    }

    protected override Task<IList<PunchoutOrderLineEntity>> LoadEntities(IRepository repository, IList<string> ids, string responseGroup)
    {
        return ((IPunchoutRepository)repository).GetPunchoutOrderLinesByIdsAsync(ids, responseGroup);
    }
}
