using System.ComponentModel.DataAnnotations;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Domain;
using VirtoCommerce.Punchout.Core.Models;

namespace VirtoCommerce.Punchout.Data.Models;

public class PunchoutUserMappingEntity : AuditableEntity, IDataEntity<PunchoutUserMappingEntity, PunchoutUserMapping>
{
    public bool IsActive { get; set; }

    [Required]
    [StringLength(256)]
    public string ExternalId { get; set; }

    [Required]
    [StringLength(128)]
    public string UserId { get; set; }

    [StringLength(256)]
    public string UserName { get; set; }

    [StringLength(128)]
    public string MemberId { get; set; }

    public virtual PunchoutUserMapping ToModel(PunchoutUserMapping model)
    {
        model.Id = Id;
        model.CreatedBy = CreatedBy;
        model.CreatedDate = CreatedDate;
        model.ModifiedBy = ModifiedBy;
        model.ModifiedDate = ModifiedDate;

        model.IsActive = IsActive;
        model.ExternalId = ExternalId;
        model.UserId = UserId;
        model.UserName = UserName;
        model.MemberId = MemberId;

        return model;
    }

    public virtual PunchoutUserMappingEntity FromModel(PunchoutUserMapping model, PrimaryKeyResolvingMap pkMap)
    {
        pkMap.AddPair(model, this);

        Id = model.Id;
        CreatedBy = model.CreatedBy;
        CreatedDate = model.CreatedDate;
        ModifiedBy = model.ModifiedBy;
        ModifiedDate = model.ModifiedDate;

        IsActive = model.IsActive;
        ExternalId = model.ExternalId;
        UserId = model.UserId;
        UserName = model.UserName;
        MemberId = model.MemberId;

        return this;
    }

    public virtual void Patch(PunchoutUserMappingEntity target)
    {
        target.IsActive = IsActive;
        target.ExternalId = ExternalId;
        target.UserId = UserId;
        target.UserName = UserName;
        target.MemberId = MemberId;
    }
}
