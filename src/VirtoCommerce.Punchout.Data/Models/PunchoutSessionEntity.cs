using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Domain;
using VirtoCommerce.Punchout.Core.Models;

namespace VirtoCommerce.Punchout.Data.Models;

public class PunchoutSessionEntity : AuditableEntity, IDataEntity<PunchoutSessionEntity, PunchoutSession>
{
    public virtual PunchoutSession ToModel(PunchoutSession model)
    {
        model.Id = Id;
        model.CreatedBy = CreatedBy;
        model.CreatedDate = CreatedDate;
        model.ModifiedBy = ModifiedBy;
        model.ModifiedDate = ModifiedDate;

        return model;
    }

    public virtual PunchoutSessionEntity FromModel(PunchoutSession model, PrimaryKeyResolvingMap pkMap)
    {
        pkMap.AddPair(model, this);

        Id = model.Id;
        CreatedBy = model.CreatedBy;
        CreatedDate = model.CreatedDate;
        ModifiedBy = model.ModifiedBy;
        ModifiedDate = model.ModifiedDate;

        return this;
    }

    public virtual void Patch(PunchoutSessionEntity target)
    {
    }
}
