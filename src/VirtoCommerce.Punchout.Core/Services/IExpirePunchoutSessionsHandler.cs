using System.Threading.Tasks;

namespace VirtoCommerce.Punchout.Core.Services;

public interface IExpirePunchoutSessionsHandler
{
    Task ExpireSessionsAsync();
}
