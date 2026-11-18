using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VirtoCommerce.Punchout.Core.Models;
using VirtoCommerce.Punchout.Core.Services;

namespace VirtoCommerce.Punchout.Data.Services;

public class PunchoutHandlerFactory(IServiceProvider serviceProvider, ILogger<PunchoutHandlerFactory> logger) : IPunchoutHandlerFactory
{
    /// <summary>
    /// Returns the last non-keyed IPunchoutHandler when the configuration has no HandlerTypeName,
    /// otherwise the IPunchoutHandler registered with HandlerTypeName as the key
    /// </summary>
    public virtual IPunchoutHandler Create(PunchoutConfiguration configuration)
    {
        var handlerTypeName = configuration?.HandlerTypeName;

        if (string.IsNullOrWhiteSpace(handlerTypeName))
        {
            return serviceProvider.GetRequiredService<IPunchoutHandler>();
        }

        var handler = serviceProvider.GetKeyedService<IPunchoutHandler>(handlerTypeName);
        if (handler == null)
        {
            logger.LogWarning("Punchout handler '{HandlerTypeName}' is not registered. Register it with ServiceCollectionExtensions.AddPunchoutHandler",
                handlerTypeName);
        }

        return handler;
    }
}
