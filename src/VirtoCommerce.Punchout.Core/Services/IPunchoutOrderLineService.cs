using VirtoCommerce.Platform.Core.GenericCrud;
using VirtoCommerce.Punchout.Core.Models;

namespace VirtoCommerce.Punchout.Core.Services;

/// <summary>
/// The requisition items ordered by the purchase orders. Saving an item that is already ordered fails on the unique index.
/// </summary>
public interface IPunchoutOrderLineService : ICrudService<PunchoutOrderLine>;
