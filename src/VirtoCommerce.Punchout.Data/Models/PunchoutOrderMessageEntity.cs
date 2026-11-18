using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Domain;
using VirtoCommerce.Punchout.Core.Models;

namespace VirtoCommerce.Punchout.Data.Models;

public class PunchoutOrderMessageEntity : AuditableEntity, IDataEntity<PunchoutOrderMessageEntity, PunchoutOrderMessage>
{
    [StringLength(128)]
    public string SessionId { get; set; }

    public string Cxml { get; set; }

    /// <summary>
    /// The snapshot of the sent cart line items.
    /// </summary>
    public virtual ObservableCollection<PunchoutRequisitionItemEntity> Items { get; set; } = new NullCollection<PunchoutRequisitionItemEntity>();

    public virtual PunchoutOrderMessage ToModel(PunchoutOrderMessage model)
    {
        model.Id = Id;
        model.CreatedBy = CreatedBy;
        model.CreatedDate = CreatedDate;
        model.ModifiedBy = ModifiedBy;
        model.ModifiedDate = ModifiedDate;

        model.Cxml = Cxml;
        model.SessionId = SessionId;
        model.Items = Items
            .Select(x => x.ToModel(AbstractTypeFactory<PunchoutRequisitionItem>.TryCreateInstance()))
            .ToList();

        return model;
    }

    public virtual PunchoutOrderMessageEntity FromModel(PunchoutOrderMessage model, PrimaryKeyResolvingMap pkMap)
    {
        pkMap.AddPair(model, this);

        Id = model.Id;
        CreatedBy = model.CreatedBy;
        CreatedDate = model.CreatedDate;
        ModifiedBy = model.ModifiedBy;
        ModifiedDate = model.ModifiedDate;

        SessionId = model.SessionId;
        Cxml = model.Cxml;

        if (model.Items != null)
        {
            Items = new ObservableCollection<PunchoutRequisitionItemEntity>(model.Items.Select(x =>
            {
                var item = AbstractTypeFactory<PunchoutRequisitionItemEntity>.TryCreateInstance().FromModel(x, pkMap);

                // Every item is from the message session
                item.OrderMessageId = Id;
                item.SessionId = SessionId;

                return item;
            }));
        }

        return this;
    }

    public virtual void Patch(PunchoutOrderMessageEntity target)
    {
        target.SessionId = SessionId;
        target.Cxml = Cxml;

        if (!Items.IsNullCollection())
        {
            Items.Patch(target.Items, (sourceItem, targetItem) => sourceItem.Patch(targetItem));
        }
    }
}
