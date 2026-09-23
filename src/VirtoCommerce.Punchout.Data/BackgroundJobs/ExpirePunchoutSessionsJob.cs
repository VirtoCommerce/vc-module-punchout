using System.Threading.Tasks;
using Hangfire;
using VirtoCommerce.Punchout.Core.Services;

namespace VirtoCommerce.Punchout.Data.BackgroundJobs;

public class ExpirePunchoutSessionsJob(IExpirePunchoutSessionsHandler handler)
{
    [DisableConcurrentExecution(10)]
    public Task Process()
    {
        return handler.ExpireSessionsAsync();
    }
}
