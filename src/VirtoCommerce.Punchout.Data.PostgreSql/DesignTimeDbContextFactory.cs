using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using VirtoCommerce.Punchout.Data.Repositories;

namespace VirtoCommerce.Punchout.Data.PostgreSql;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<PunchoutDbContext>
{
    public PunchoutDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<PunchoutDbContext>();
        var connectionString = args.Length != 0 ? args[0] : "Server=localhost;Username=virto;Password=virto;Database=VirtoCommerce3;";

        builder.UseNpgsql(
            connectionString,
            options => options.MigrationsAssembly(typeof(PostgreSqlDataAssemblyMarker).Assembly.GetName().Name));

        return new PunchoutDbContext(builder.Options);
    }
}
