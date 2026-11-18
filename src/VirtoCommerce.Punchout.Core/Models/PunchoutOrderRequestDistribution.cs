using System.Collections.Generic;

namespace VirtoCommerce.Punchout.Core.Models;

/// <summary>
/// Buyer accounting allocation of a line item charge.
/// </summary>
public class PunchoutOrderRequestDistribution
{
    public string AccountingName { get; set; }

    public IList<PunchoutAccountingSegment> Segments { get; set; } = [];

    public decimal? ChargeAmount { get; set; }

    public string Currency { get; set; }
}
