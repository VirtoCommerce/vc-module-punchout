using VirtoCommerce.StoreModule.Core.Model;

namespace VirtoCommerce.Punchout.Core.Models;

public class PunchoutOrderRequestHandlerContext : PunchoutHandlerContext
{
    public PunchoutOrderRequestData Request { get; set; }

    public PunchoutUserMapping UserMapping { get; set; }

    public Store Store { get; set; }

    public PunchoutOrderResult Result { get; set; }
}
