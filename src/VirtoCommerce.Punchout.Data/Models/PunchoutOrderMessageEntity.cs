using System.ComponentModel.DataAnnotations;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Domain;
using VirtoCommerce.Punchout.Core.Models;

namespace VirtoCommerce.Punchout.Data.Models;

public class PunchoutOrderMessageEntity : AuditableEntity, IDataEntity<PunchoutOrderMessageEntity, PunchoutOrderMessage>
{
    [StringLength(128)]
    public string SessionId { get; set; }

    public string Cxml { get; set; }

    public virtual PunchoutOrderMessage ToModel(PunchoutOrderMessage model)
    {
        model.Id = Id;
        model.CreatedBy = CreatedBy;
        model.CreatedDate = CreatedDate;
        model.ModifiedBy = ModifiedBy;
        model.ModifiedDate = ModifiedDate;

        model.Cxml = Cxml;
        model.SessionId = SessionId;

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

        return this;
    }

    public virtual void Patch(PunchoutOrderMessageEntity target)
    {
        target.SessionId = SessionId;
        target.Cxml = Cxml;
    }
}
