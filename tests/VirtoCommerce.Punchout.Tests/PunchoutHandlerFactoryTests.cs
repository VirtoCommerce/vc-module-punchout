using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using VirtoCommerce.Punchout.Core.Extensions;
using VirtoCommerce.Punchout.Core.Models;
using VirtoCommerce.Punchout.Core.Services;
using VirtoCommerce.Punchout.Data.Services;
using Xunit;

namespace VirtoCommerce.Punchout.Tests;

[Trait("Category", "Unit")]
public class PunchoutHandlerFactoryTests
{
    [Fact]
    public void Create_NoHandlerTypeName_ReturnsLastRegisteredHandler()
    {
        var factory = CreateFactory(services => services
            .AddTransient<IPunchoutHandler, FirstHandler>()
            .AddTransient<IPunchoutHandler, SecondHandler>()
            .AddPunchoutHandler<ThirdHandler>());

        var handler = factory.Create(new PunchoutConfiguration());

        Assert.IsType<SecondHandler>(handler);
    }

    [Fact]
    public void Create_HandlerTypeName_ReturnsKeyedHandler()
    {
        var factory = CreateFactory(services => services
            .AddTransient<IPunchoutHandler, FirstHandler>()
            .AddPunchoutHandler<SecondHandler>()
            .AddPunchoutHandler<ThirdHandler>("custom"));

        Assert.IsType<SecondHandler>(factory.Create(new PunchoutConfiguration { HandlerTypeName = nameof(SecondHandler) }));
        Assert.IsType<ThirdHandler>(factory.Create(new PunchoutConfiguration { HandlerTypeName = "custom" }));
    }

    [Fact]
    public void Create_UnknownHandlerTypeName_ReturnsNull()
    {
        var factory = CreateFactory(services => services.AddTransient<IPunchoutHandler, FirstHandler>());

        var handler = factory.Create(new PunchoutConfiguration { HandlerTypeName = nameof(FirstHandler) });

        Assert.Null(handler);
    }

    private static PunchoutHandlerFactory CreateFactory(Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        configure(services);

        return new PunchoutHandlerFactory(services.BuildServiceProvider(), NullLogger<PunchoutHandlerFactory>.Instance);
    }

    private class FirstHandler : IPunchoutHandler
    {
        public Task HandleSetupAsync(PunchoutSetupHandlerContext context) => Task.CompletedTask;

        public Task HandleOrderMessageAsync(PunchoutOrderMessageHandlerContext context) => Task.CompletedTask;
    }

    private sealed class SecondHandler : FirstHandler;

    private sealed class ThirdHandler : FirstHandler;
}
