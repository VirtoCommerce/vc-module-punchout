using System.Collections.Generic;

namespace VirtoCommerce.Punchout.Core.Models;

public class PunchoutOrderRequestItem
{
    public int LineNumber { get; set; }

    public decimal Quantity { get; set; }

    public string SupplierPartId { get; set; }

    /// <summary>
    /// Opaque value returned to the buyer in the PunchOutOrderMessage, sent back unchanged.
    /// </summary>
    public string SupplierPartAuxiliaryId { get; set; }

    public decimal? UnitPrice { get; set; }

    public string Currency { get; set; }

    public string Description { get; set; }

    public string UnitOfMeasure { get; set; }

    /// <summary>
    /// Classification values keyed by domain, e.g. 'UNSPSC'.
    /// </summary>
    public IDictionary<string, string> Classifications { get; set; }

    public IList<PunchoutOrderRequestDistribution> Distributions { get; set; } = [];
}
