using Microsoft.Extensions.DependencyInjection;
using VirtoCommerce.Punchout.Core.Services;

namespace VirtoCommerce.Punchout.Core.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPunchoutHandler<THandler>(this IServiceCollection services, string handlerTypeName = null)
        where THandler : class, IPunchoutHandler
    {
        services.AddKeyedTransient<IPunchoutHandler, THandler>(handlerTypeName ?? typeof(THandler).Name);

        return services;
    }
}
