using System.Threading.Tasks;
using VirtoCommerce.Punchout.Core.Models;

namespace VirtoCommerce.Punchout.ExperienceApi.Services;

/// <summary>
/// Creates, updates and cancels the customer order of a purchase order. A rejected order is reported with <see cref="PunchoutHandlerContext.Fail"/>,
/// the created or changed customer order is set in <see cref="PunchoutOrderRequestHandlerContext.Result"/>.
/// </summary>
public interface IPunchoutCustomerOrderService
{
    /// <summary>
    /// Creates the customer order from the requisition items matched with the order lines.
    /// </summary>
    Task CreateOrderAsync(PunchoutOrderRequestHandlerContext context);

    /// <summary>
    /// Applies a new version of the purchase order to the customer order created by the previous version.
    /// </summary>
    Task UpdateOrderAsync(PunchoutOrderRequestHandlerContext context);

    /// <summary>
    /// Cancels the customer order created by the previous version.
    /// </summary>
    Task CancelOrderAsync(PunchoutOrderRequestHandlerContext context);
}
