using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
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
    /// The snapshot of the sent cart line items in JSON.
    /// </summary>
    public string Items { get; set; }

    public virtual PunchoutOrderMessage ToModel(PunchoutOrderMessage model)
    {
        model.Id = Id;
        model.CreatedBy = CreatedBy;
        model.CreatedDate = CreatedDate;
        model.ModifiedBy = ModifiedBy;
        model.ModifiedDate = ModifiedDate;

        model.Cxml = Cxml;
        model.SessionId = SessionId;
        model.Items = DeserializeItems(Items);

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
        Items = model.Items.IsNullOrEmpty() ? null : JsonSerializer.Serialize(model.Items);

        return this;
    }

    public virtual void Patch(PunchoutOrderMessageEntity target)
    {
        target.SessionId = SessionId;
        target.Cxml = Cxml;
        target.Items = Items;
    }

    protected virtual IList<PunchoutRequisitionItem> DeserializeItems(string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return [];
        }

        var items = JsonSerializer.Deserialize<List<PunchoutRequisitionItem>>(json) ?? [];

        // The session id is not repeated in the snapshot, every item is from the message session
        foreach (var item in items)
        {
            item.SessionId = SessionId;
        }

        return items;
    }
}
