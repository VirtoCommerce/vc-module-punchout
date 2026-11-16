using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Domain;
using VirtoCommerce.Punchout.Core;
using VirtoCommerce.Punchout.Core.Models;

namespace VirtoCommerce.Punchout.Data.Models;

public class PunchoutOrderRequestEntity : AuditableEntity, IDataEntity<PunchoutOrderRequestEntity, PunchoutOrderRequest>
{
    [Required]
    [StringLength(ModuleConstants.FieldLength.Id)]
    public string ConfigurationId { get; set; }

    [StringLength(ModuleConstants.FieldLength.Id)]
    public string StoreId { get; set; }

    [Required]
    [StringLength(ModuleConstants.FieldLength.PayloadId)]
    public string PayloadId { get; set; }

    [StringLength(ModuleConstants.FieldLength.Identity)]
    public string SenderIdentity { get; set; }

    [StringLength(ModuleConstants.FieldLength.Identity)]
    public string BuyerIdentity { get; set; }

    [StringLength(ModuleConstants.FieldLength.Id)]
    public string UserId { get; set; }

    [StringLength(ModuleConstants.FieldLength.OrderType)]
    public string OrderType { get; set; }

    [StringLength(ModuleConstants.FieldLength.OrderVersion)]
    public string OrderVersion { get; set; }

    [StringLength(ModuleConstants.FieldLength.PayloadId)]
    public string PreviousPayloadId { get; set; }

    public string Cxml { get; set; }

    [StringLength(ModuleConstants.FieldLength.PurchaseOrderNumber)]
    public string PurchaseOrderNumber { get; set; }

    public DateTime? PurchaseOrderDate { get; set; }

    [StringLength(ModuleConstants.FieldLength.Currency)]
    public string Currency { get; set; }

    [Precision(19, 4)]
    public decimal? Total { get; set; }

    [StringLength(ModuleConstants.FieldLength.Id)]
    public string CustomerOrderId { get; set; }

    [Required]
    [StringLength(ModuleConstants.FieldLength.Status)]
    public string Status { get; set; }

    [StringLength(ModuleConstants.FieldLength.ErrorCode)]
    public string ErrorCode { get; set; }

    [StringLength(ModuleConstants.FieldLength.ErrorMessage)]
    public string ErrorMessage { get; set; }

    public virtual PunchoutOrderRequest ToModel(PunchoutOrderRequest model)
    {
        model.Id = Id;
        model.CreatedBy = CreatedBy;
        model.CreatedDate = CreatedDate;
        model.ModifiedBy = ModifiedBy;
        model.ModifiedDate = ModifiedDate;

        model.ConfigurationId = ConfigurationId;
        model.StoreId = StoreId;
        model.PayloadId = PayloadId;
        model.SenderIdentity = SenderIdentity;
        model.BuyerIdentity = BuyerIdentity;
        model.UserId = UserId;
        model.OrderType = OrderType;
        model.OrderVersion = OrderVersion;
        model.PreviousPayloadId = PreviousPayloadId;
        model.Cxml = Cxml;
        model.PurchaseOrderNumber = PurchaseOrderNumber;
        model.PurchaseOrderDate = PurchaseOrderDate;
        model.Currency = Currency;
        model.Total = Total;
        model.CustomerOrderId = CustomerOrderId;
        model.Status = Status;
        model.ErrorCode = ErrorCode;
        model.ErrorMessage = ErrorMessage;

        return model;
    }

    public virtual PunchoutOrderRequestEntity FromModel(PunchoutOrderRequest model, PrimaryKeyResolvingMap pkMap)
    {
        pkMap.AddPair(model, this);

        Id = model.Id;
        CreatedBy = model.CreatedBy;
        CreatedDate = model.CreatedDate;
        ModifiedBy = model.ModifiedBy;
        ModifiedDate = model.ModifiedDate;

        ConfigurationId = model.ConfigurationId;
        StoreId = model.StoreId;
        PayloadId = model.PayloadId;
        SenderIdentity = model.SenderIdentity;
        BuyerIdentity = model.BuyerIdentity;
        UserId = model.UserId;
        OrderType = model.OrderType;
        OrderVersion = model.OrderVersion;
        PreviousPayloadId = model.PreviousPayloadId;
        Cxml = model.Cxml;
        PurchaseOrderNumber = model.PurchaseOrderNumber;
        PurchaseOrderDate = model.PurchaseOrderDate;
        Currency = model.Currency;
        Total = model.Total;
        CustomerOrderId = model.CustomerOrderId;
        Status = model.Status;
        ErrorCode = model.ErrorCode;
        ErrorMessage = model.ErrorMessage;

        return this;
    }

    public virtual void Patch(PunchoutOrderRequestEntity target)
    {
        target.ConfigurationId = ConfigurationId;
        target.StoreId = StoreId;
        target.PayloadId = PayloadId;
        target.SenderIdentity = SenderIdentity;
        target.BuyerIdentity = BuyerIdentity;
        target.UserId = UserId;
        target.OrderType = OrderType;
        target.OrderVersion = OrderVersion;
        target.PreviousPayloadId = PreviousPayloadId;
        target.Cxml = Cxml;
        target.PurchaseOrderNumber = PurchaseOrderNumber;
        target.PurchaseOrderDate = PurchaseOrderDate;
        target.Currency = Currency;
        target.Total = Total;
        target.CustomerOrderId = CustomerOrderId;
        target.Status = Status;
        target.ErrorCode = ErrorCode;
        target.ErrorMessage = ErrorMessage;
    }
}
