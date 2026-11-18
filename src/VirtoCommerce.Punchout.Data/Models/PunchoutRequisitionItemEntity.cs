using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Domain;
using VirtoCommerce.Punchout.Core;
using VirtoCommerce.Punchout.Core.Models;

namespace VirtoCommerce.Punchout.Data.Models;

public class PunchoutRequisitionItemEntity : Entity, IDataEntity<PunchoutRequisitionItemEntity, PunchoutRequisitionItem>
{
    [Required]
    [StringLength(ModuleConstants.FieldLength.Id)]
    public string OrderMessageId { get; set; }

    public virtual PunchoutOrderMessageEntity OrderMessage { get; set; }

    /// <summary>
    /// The session of the order message, stored to find the items of the returned sessions.
    /// </summary>
    [Required]
    [StringLength(ModuleConstants.FieldLength.Id)]
    public string SessionId { get; set; }

    [Required]
    [StringLength(ModuleConstants.FieldLength.Id)]
    public string LineItemId { get; set; }

    [StringLength(ModuleConstants.FieldLength.Id)]
    public string ProductId { get; set; }

    [StringLength(ModuleConstants.FieldLength.Id)]
    public string CatalogId { get; set; }

    [StringLength(ModuleConstants.FieldLength.Id)]
    public string CategoryId { get; set; }

    [StringLength(ModuleConstants.FieldLength.Sku)]
    public string Sku { get; set; }

    [StringLength(ModuleConstants.FieldLength.ProductName)]
    public string Name { get; set; }

    [StringLength(ModuleConstants.FieldLength.ImageUrl)]
    public string ImageUrl { get; set; }

    [StringLength(ModuleConstants.FieldLength.ProductType)]
    public string ProductType { get; set; }

    [StringLength(ModuleConstants.FieldLength.Id)]
    public string ProductOuterId { get; set; }

    [StringLength(ModuleConstants.FieldLength.Id)]
    public string PriceId { get; set; }

    public bool IsGift { get; set; }

    [StringLength(ModuleConstants.FieldLength.Id)]
    public string FulfillmentCenterId { get; set; }

    [StringLength(ModuleConstants.FieldLength.Id)]
    public string FulfillmentCenterName { get; set; }

    [StringLength(ModuleConstants.FieldLength.Unit)]
    public string WeightUnit { get; set; }

    [Precision(18, 4)]
    public decimal? Weight { get; set; }

    [StringLength(ModuleConstants.FieldLength.Unit)]
    public string MeasureUnit { get; set; }

    [Precision(18, 4)]
    public decimal? Height { get; set; }

    [Precision(18, 4)]
    public decimal? Length { get; set; }

    [Precision(18, 4)]
    public decimal? Width { get; set; }

    public int Quantity { get; set; }

    [StringLength(ModuleConstants.FieldLength.Currency)]
    public string Currency { get; set; }

    [Precision(18, 4)]
    public decimal ListPrice { get; set; }

    [Precision(18, 4)]
    public decimal SalePrice { get; set; }

    [Precision(18, 4)]
    public decimal DiscountAmount { get; set; }

    [Precision(18, 4)]
    public decimal PlacedPrice { get; set; }

    [StringLength(ModuleConstants.FieldLength.TaxType)]
    public string TaxType { get; set; }

    [Precision(18, 4)]
    public decimal TaxPercentRate { get; set; }

    public virtual PunchoutRequisitionItem ToModel(PunchoutRequisitionItem model)
    {
        model.Id = Id;
        model.OrderMessageId = OrderMessageId;
        model.SessionId = SessionId;
        model.LineItemId = LineItemId;
        model.ProductId = ProductId;
        model.CatalogId = CatalogId;
        model.CategoryId = CategoryId;
        model.Sku = Sku;
        model.Name = Name;
        model.ImageUrl = ImageUrl;
        model.ProductType = ProductType;
        model.ProductOuterId = ProductOuterId;
        model.PriceId = PriceId;
        model.IsGift = IsGift;
        model.FulfillmentCenterId = FulfillmentCenterId;
        model.FulfillmentCenterName = FulfillmentCenterName;
        model.WeightUnit = WeightUnit;
        model.Weight = Weight;
        model.MeasureUnit = MeasureUnit;
        model.Height = Height;
        model.Length = Length;
        model.Width = Width;
        model.Quantity = Quantity;
        model.Currency = Currency;
        model.ListPrice = ListPrice;
        model.SalePrice = SalePrice;
        model.DiscountAmount = DiscountAmount;
        model.PlacedPrice = PlacedPrice;
        model.TaxType = TaxType;
        model.TaxPercentRate = TaxPercentRate;

        return model;
    }

    public virtual PunchoutRequisitionItemEntity FromModel(PunchoutRequisitionItem model, PrimaryKeyResolvingMap pkMap)
    {
        pkMap.AddPair(model, this);

        Id = model.Id;
        OrderMessageId = model.OrderMessageId;
        SessionId = model.SessionId;
        LineItemId = model.LineItemId;
        ProductId = model.ProductId;
        CatalogId = model.CatalogId;
        CategoryId = model.CategoryId;
        Sku = model.Sku;
        Name = model.Name;
        ImageUrl = model.ImageUrl;
        ProductType = model.ProductType;
        ProductOuterId = model.ProductOuterId;
        PriceId = model.PriceId;
        IsGift = model.IsGift;
        FulfillmentCenterId = model.FulfillmentCenterId;
        FulfillmentCenterName = model.FulfillmentCenterName;
        WeightUnit = model.WeightUnit;
        Weight = model.Weight;
        MeasureUnit = model.MeasureUnit;
        Height = model.Height;
        Length = model.Length;
        Width = model.Width;
        Quantity = model.Quantity;
        Currency = model.Currency;
        ListPrice = model.ListPrice;
        SalePrice = model.SalePrice;
        DiscountAmount = model.DiscountAmount;
        PlacedPrice = model.PlacedPrice;
        TaxType = model.TaxType;
        TaxPercentRate = model.TaxPercentRate;

        return this;
    }

    /// <summary>
    /// The snapshot does not change after the order message is sent.
    /// </summary>
    public virtual void Patch(PunchoutRequisitionItemEntity target)
    {
    }
}
