using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Punchout.Core.Models;
using VirtoCommerce.Punchout.Core.Services;
using VirtoCommerce.Punchout.Data.Repositories;

namespace VirtoCommerce.Punchout.Data.Services;

public class PunchoutRequisitionItemService(Func<IPunchoutRepository> repositoryFactory) : IPunchoutRequisitionItemService
{
    public virtual async Task<IList<PunchoutRequisitionItem>> GetReturnedItemsAsync(string configurationId, string userId, IList<string> lineItemIds)
    {
        if (string.IsNullOrEmpty(configurationId) || string.IsNullOrEmpty(userId) || lineItemIds.IsNullOrEmpty())
        {
            return [];
        }

        using var repository = repositoryFactory();

        var entities = await repository.GetReturnedRequisitionItemsAsync(configurationId, userId, lineItemIds);

        return entities
            .Select(x => x.ToModel(AbstractTypeFactory<PunchoutRequisitionItem>.TryCreateInstance()))
            .ToList();
    }
}
