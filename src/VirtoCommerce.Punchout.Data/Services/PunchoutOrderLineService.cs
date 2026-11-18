using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Punchout.Core.Models;
using VirtoCommerce.Punchout.Core.Services;
using VirtoCommerce.Punchout.Data.Models;
using VirtoCommerce.Punchout.Data.Repositories;

namespace VirtoCommerce.Punchout.Data.Services;

/// <summary>
/// The ordered items are read and written without the cache, the unique index decides which purchase order orders an item.
/// </summary>
public class PunchoutOrderLineService(Func<IPunchoutRepository> repositoryFactory) : IPunchoutOrderLineService
{
    public virtual async Task<IList<PunchoutOrderLine>> GetByLineItemIdsAsync(string configurationId, IList<string> lineItemIds)
    {
        if (string.IsNullOrEmpty(configurationId) || lineItemIds.IsNullOrEmpty())
        {
            return [];
        }

        using var repository = repositoryFactory();

        var entities = await repository.PunchoutOrderLines
            .Where(x => x.ConfigurationId == configurationId && lineItemIds.Contains(x.LineItemId))
            .AsNoTracking()
            .ToListAsync();

        return ToModels(entities);
    }

    public virtual async Task<IList<PunchoutOrderLine>> GetByPurchaseOrderAsync(string configurationId, string purchaseOrderNumber)
    {
        if (string.IsNullOrEmpty(configurationId) || string.IsNullOrEmpty(purchaseOrderNumber))
        {
            return [];
        }

        using var repository = repositoryFactory();

        var entities = await repository.PunchoutOrderLines
            .Where(x => x.ConfigurationId == configurationId && x.PurchaseOrderNumber == purchaseOrderNumber)
            .AsNoTracking()
            .ToListAsync();

        return ToModels(entities);
    }

    public virtual async Task<bool> TryAddAsync(IList<PunchoutOrderLine> lines)
    {
        if (lines.IsNullOrEmpty())
        {
            return true;
        }

        using var repository = repositoryFactory();

        var pkMap = new PrimaryKeyResolvingMap();

        foreach (var line in lines)
        {
            var entity = AbstractTypeFactory<PunchoutOrderLineEntity>.TryCreateInstance().FromModel(line, pkMap);
            repository.Add(entity);
        }

        try
        {
            await repository.UnitOfWork.CommitAsync();
        }
        catch (DbUpdateException)
        {
            // The unique index rejects an item ordered by another purchase order, nothing is stored
            return false;
        }

        pkMap.ResolvePrimaryKeys();

        return true;
    }

    public virtual async Task DeleteAsync(IList<string> ids)
    {
        if (ids.IsNullOrEmpty())
        {
            return;
        }

        using var repository = repositoryFactory();

        await repository.DeletePunchoutOrderLinesAsync(ids);
    }

    protected virtual IList<PunchoutOrderLine> ToModels(IList<PunchoutOrderLineEntity> entities)
    {
        return entities
            .Select(x => x.ToModel(AbstractTypeFactory<PunchoutOrderLine>.TryCreateInstance()))
            .ToList();
    }
}
