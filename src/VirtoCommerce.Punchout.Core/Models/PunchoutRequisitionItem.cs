using System;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.Punchout.Core.Models;

/// <summary>
/// A cart line item as it was sent to the buyer in a PunchOutOrderMessage.
/// The snapshot is stored with the order message, the customer order is created from it, not from the cart.
/// </summary>
public class PunchoutRequisitionItem : Entity, ICloneable
{
    public string OrderMessageId { get; set; }

    /// <summary>
    /// The session that returned the item, the session of the order message.
    /// </summary>
    public string SessionId { get; set; }

    /// <summary>
    /// The cart line item id, sent in the SupplierPartAuxiliaryID.
    /// </summary>
    public string LineItemId { get; set; }

    public string ProductId { get; set; }

    public string CatalogId { get; set; }

    public string CategoryId { get; set; }

    public string Sku { get; set; }

    public string Name { get; set; }

    public string ImageUrl { get; set; }

    public string ProductType { get; set; }

    public string ProductOuterId { get; set; }

    public string PriceId { get; set; }

    public bool IsGift { get; set; }

    public string FulfillmentCenterId { get; set; }

    public string FulfillmentCenterName { get; set; }

    public string WeightUnit { get; set; }

    public decimal? Weight { get; set; }

    /// <summary>
    /// The unit of the dimensions.
    /// </summary>
    public string MeasureUnit { get; set; }

    public decimal? Height { get; set; }

    public decimal? Length { get; set; }

    public decimal? Width { get; set; }

    public int Quantity { get; set; }

    public string Currency { get; set; }

    public decimal ListPrice { get; set; }

    public decimal SalePrice { get; set; }

    /// <summary>
    /// The discount per unit.
    /// </summary>
    public decimal DiscountAmount { get; set; }

    /// <summary>
    /// The unit price sent to the buyer.
    /// </summary>
    public decimal PlacedPrice { get; set; }

    public string TaxType { get; set; }

    public decimal TaxPercentRate { get; set; }

    public virtual object Clone()
    {
        return MemberwiseClone();
    }
}
