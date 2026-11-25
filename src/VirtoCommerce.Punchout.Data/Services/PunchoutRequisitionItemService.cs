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
    public virtual async Task<IList<PunchoutRequisitionItem>> GetReturnedItemsAsync(PunchoutReturnedItemsCriteria criteria)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        if (string.IsNullOrEmpty(criteria.ConfigurationId) ||
            (string.IsNullOrEmpty(criteria.UserId) && string.IsNullOrEmpty(criteria.OrganizationId)) ||
            criteria.LineItemIds.IsNullOrEmpty())
        {
            return [];
        }

        using var repository = repositoryFactory();

        var entities = await repository.GetReturnedRequisitionItemsAsync(criteria.ConfigurationId, criteria.UserId, criteria.OrganizationId, criteria.LineItemIds);

        return entities
            .Select(x => x.ToModel(AbstractTypeFactory<PunchoutRequisitionItem>.TryCreateInstance()))
            .ToList();
    }
}
