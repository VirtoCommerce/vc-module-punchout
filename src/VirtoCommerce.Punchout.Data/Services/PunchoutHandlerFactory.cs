using System;
using Microsoft.Extensions.DependencyInjection;
using VirtoCommerce.Punchout.Core;
using VirtoCommerce.Punchout.Core.Extensions;
using VirtoCommerce.Punchout.Core.Models;
using VirtoCommerce.Punchout.Core.Services;

namespace VirtoCommerce.Punchout.Data.Services;

public class PunchoutHandlerFactory(IServiceProvider serviceProvider) : IPunchoutHandlerFactory
{
    /// <summary>
    /// The handler used when the configuration has no HandlerTypeName
    /// </summary>
    protected virtual string DefaultHandlerTypeName => ModuleConstants.DefaultPunchoutHandlerName;

    public virtual IPunchoutHandler Create(PunchoutConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var handlerTypeName = string.IsNullOrEmpty(configuration.HandlerTypeName)
            ? DefaultHandlerTypeName
            : configuration.HandlerTypeName;

        return serviceProvider.GetKeyedService<IPunchoutHandler>(handlerTypeName)
               ?? throw new InvalidOperationException(
                   $"Punchout handler '{handlerTypeName}' is not registered. Register it with {nameof(ServiceCollectionExtensions.AddPunchoutHandler)}.");
    }
}
