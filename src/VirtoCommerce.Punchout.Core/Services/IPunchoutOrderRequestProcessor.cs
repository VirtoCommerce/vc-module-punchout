using System.Threading.Tasks;
using VirtoCommerce.Punchout.Core.Models;

namespace VirtoCommerce.Punchout.Core.Services;

public interface IPunchoutOrderRequestProcessor
{
    Task<PunchoutOrderResult> ProcessAsync(PunchoutOrderRequestData request);
}
