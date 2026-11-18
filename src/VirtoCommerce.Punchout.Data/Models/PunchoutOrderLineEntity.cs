using System.ComponentModel.DataAnnotations;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Domain;
using VirtoCommerce.Punchout.Core;
using VirtoCommerce.Punchout.Core.Models;

namespace VirtoCommerce.Punchout.Data.Models;

public class PunchoutOrderLineEntity : AuditableEntity, IDataEntity<PunchoutOrderLineEntity, PunchoutOrderLine>
{
    [Required]
    [StringLength(ModuleConstants.FieldLength.Id)]
    public string ConfigurationId { get; set; }

    [Required]
    [StringLength(ModuleConstants.FieldLength.Id)]
    public string SessionId { get; set; }

    [Required]
    [StringLength(ModuleConstants.FieldLength.Id)]
    public string LineItemId { get; set; }

    [Required]
    [StringLength(ModuleConstants.FieldLength.PurchaseOrderNumber)]
    public string PurchaseOrderNumber { get; set; }

    [StringLength(ModuleConstants.FieldLength.LineNumber)]
    public string LineNumber { get; set; }

    [StringLength(ModuleConstants.FieldLength.Id)]
    public string OrderRequestId { get; set; }

    public virtual PunchoutOrderLine ToModel(PunchoutOrderLine model)
    {
        model.Id = Id;
        model.CreatedBy = CreatedBy;
        model.CreatedDate = CreatedDate;
        model.ModifiedBy = ModifiedBy;
        model.ModifiedDate = ModifiedDate;

        model.ConfigurationId = ConfigurationId;
        model.SessionId = SessionId;
        model.LineItemId = LineItemId;
        model.PurchaseOrderNumber = PurchaseOrderNumber;
        model.LineNumber = LineNumber;
        model.OrderRequestId = OrderRequestId;

        return model;
    }

    public virtual PunchoutOrderLineEntity FromModel(PunchoutOrderLine model, PrimaryKeyResolvingMap pkMap)
    {
        pkMap.AddPair(model, this);

        Id = model.Id;
        CreatedBy = model.CreatedBy;
        CreatedDate = model.CreatedDate;
        ModifiedBy = model.ModifiedBy;
        ModifiedDate = model.ModifiedDate;

        ConfigurationId = model.ConfigurationId;
        SessionId = model.SessionId;
        LineItemId = model.LineItemId;
        PurchaseOrderNumber = model.PurchaseOrderNumber;
        LineNumber = model.LineNumber;
        OrderRequestId = model.OrderRequestId;

        return this;
    }

    public virtual void Patch(PunchoutOrderLineEntity target)
    {
        target.ConfigurationId = ConfigurationId;
        target.SessionId = SessionId;
        target.LineItemId = LineItemId;
        target.PurchaseOrderNumber = PurchaseOrderNumber;
        target.LineNumber = LineNumber;
        target.OrderRequestId = OrderRequestId;
    }
}
