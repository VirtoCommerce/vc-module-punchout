using System.Threading.Tasks;

namespace VirtoCommerce.Punchout.Core.Services;

public interface IExpirePunchoutSessionsHandler
{
    /// <summary>
    /// Moves the sessions whose expiration date has passed to the Expired status.
    /// </summary>
    Task ExpireSessionsAsync();
}
