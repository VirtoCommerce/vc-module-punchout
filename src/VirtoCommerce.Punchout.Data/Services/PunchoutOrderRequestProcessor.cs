using System;
using System.Threading.Tasks;
using VirtoCommerce.Punchout.Core.Models;
using VirtoCommerce.Punchout.Core.Services;

namespace VirtoCommerce.Punchout.Data.Services;

public class PunchoutOrderRequestProcessor : IPunchoutOrderRequestProcessor
{
    public Task<PunchoutOrderResult> ProcessAsync(PunchoutOrderRequest request)
    {
        throw new NotImplementedException();
    }
}
