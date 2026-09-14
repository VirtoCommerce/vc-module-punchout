using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.Platform.Data.MySql.Extensions;
using VirtoCommerce.Platform.Data.PostgreSql.Extensions;
using VirtoCommerce.Platform.Data.SqlServer.Extensions;
using VirtoCommerce.Punchout.Core;
using VirtoCommerce.Punchout.Core.Services;
using VirtoCommerce.Punchout.Data.MySql;
using VirtoCommerce.Punchout.Data.PostgreSql;
using VirtoCommerce.Punchout.Data.Repositories;
using VirtoCommerce.Punchout.Data.Services;
using VirtoCommerce.Punchout.Data.SqlServer;

namespace VirtoCommerce.Punchout.Web;

public class Module : IModule, IHasConfiguration
{
    public ManifestModuleInfo ModuleInfo { get; set; }
    public IConfiguration Configuration { get; set; }

    public void Initialize(IServiceCollection serviceCollection)
    {
        serviceCollection.AddDbContext<PunchoutDbContext>(options =>
        {
            var databaseProvider = Configuration.GetValue("DatabaseProvider", "SqlServer");
            var connectionString = Configuration.GetConnectionString(ModuleInfo.Id) ?? Configuration.GetConnectionString("VirtoCommerce");

            switch (databaseProvider)
            {
                case "MySql":
                    options.UseMySqlDatabase(connectionString, typeof(MySqlDataAssemblyMarker), Configuration);
                    break;
                case "PostgreSql":
                    options.UsePostgreSqlDatabase(connectionString, typeof(PostgreSqlDataAssemblyMarker), Configuration);
                    break;
                default:
                    options.UseSqlServerDatabase(connectionString, typeof(SqlServerDataAssemblyMarker), Configuration);
                    break;
            }
        });

        // Register services
        serviceCollection.AddTransient<IPunchoutRepository, PunchoutRepository>();
        serviceCollection.AddSingleton<Func<IPunchoutRepository>>(provider => () => provider.CreateScope().ServiceProvider.GetRequiredService<IPunchoutRepository>());

        serviceCollection.AddTransient<IPunchoutSessionService, PunchoutSessionService>();
        serviceCollection.AddTransient<IPunchoutSessionSearchService, PunchoutSessionSearchService>();

        serviceCollection.AddTransient<IPunchoutIntegrationService, PunchoutIntegrationService>();
        serviceCollection.AddTransient<IPunchoutIntegrationSearchService, PunchoutIntegrationSearchService>();
    }

    public void PostInitialize(IApplicationBuilder appBuilder)
    {
        var serviceProvider = appBuilder.ApplicationServices;

        // Register settings
        var settingsRegistrar = serviceProvider.GetRequiredService<ISettingsRegistrar>();
        settingsRegistrar.RegisterSettings(ModuleConstants.Settings.AllSettings, ModuleInfo.Id);

        // Register permissions
        var permissionsRegistrar = serviceProvider.GetRequiredService<IPermissionsRegistrar>();
        permissionsRegistrar.RegisterPermissions(ModuleInfo.Id, "Punchout", ModuleConstants.Security.Permissions.AllPermissions);

        // Apply migrations
        using var serviceScope = serviceProvider.CreateScope();
        using var dbContext = serviceScope.ServiceProvider.GetRequiredService<PunchoutDbContext>();
        dbContext.Database.Migrate();
    }

    public void Uninstall()
    {
        // Nothing to do here
    }
}
