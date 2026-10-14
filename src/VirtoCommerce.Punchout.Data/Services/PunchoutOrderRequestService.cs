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

public class PunchoutOrderRequestService(
    Func<IPunchoutRepository> repositoryFactory,
    IPlatformMemoryCache platformMemoryCache,
    IEventPublisher eventPublisher)
    : CrudService<PunchoutOrderRequest, PunchoutOrderRequestEntity, PunchoutOrderRequestChangingEvent, PunchoutOrderRequestChangedEvent>
        (repositoryFactory, platformMemoryCache, eventPublisher),
        IPunchoutOrderRequestService
{
    protected override Task<IList<PunchoutOrderRequestEntity>> LoadEntities(IRepository repository, IList<string> ids, string responseGroup)
    {
        return ((IPunchoutRepository)repository).GetPunchoutOrderRequestsByIdsAsync(ids, responseGroup);
    }
}
