using System.ComponentModel.DataAnnotations;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Domain;
using VirtoCommerce.Punchout.Core.Models;

namespace VirtoCommerce.Punchout.Data.Models;

public class PunchoutIntegrationEntity : AuditableEntity, IDataEntity<PunchoutIntegrationEntity, PunchoutIntegration>
{
    public bool IsActive { get; set; }

    [StringLength(256)]
    public string Name { get; set; }

    [StringLength(128)]
    public string StoreId { get; set; }

    [StringLength(256)]
    public string CredentialDomain { get; set; }

    [StringLength(512)]
    public string SenderIdentity { get; set; }

    public string AllowedReturnUrls { get; set; }

    public virtual PunchoutIntegration ToModel(PunchoutIntegration model)
    {
        model.Id = Id;
        model.CreatedBy = CreatedBy;
        model.CreatedDate = CreatedDate;
        model.ModifiedBy = ModifiedBy;
        model.ModifiedDate = ModifiedDate;

        return model;
    }

    public virtual PunchoutIntegrationEntity FromModel(PunchoutIntegration model, PrimaryKeyResolvingMap pkMap)
    {
        pkMap.AddPair(model, this);

        Id = model.Id;
        CreatedBy = model.CreatedBy;
        CreatedDate = model.CreatedDate;
        ModifiedBy = model.ModifiedBy;
        ModifiedDate = model.ModifiedDate;

        return this;
    }

    public virtual void Patch(PunchoutIntegrationEntity target)
    {
    }
}
