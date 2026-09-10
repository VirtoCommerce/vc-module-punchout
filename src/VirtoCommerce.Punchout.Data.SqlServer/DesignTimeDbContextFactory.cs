using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using VirtoCommerce.Punchout.Data.Repositories;

namespace VirtoCommerce.Punchout.Data.SqlServer;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<PunchoutDbContext>
{
    public PunchoutDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<PunchoutDbContext>();
        var connectionString = args.Length != 0 ? args[0] : "Server=(local);User=virto;Password=virto;Database=VirtoCommerce3;";

        builder.UseSqlServer(
            connectionString,
            options => options.MigrationsAssembly(typeof(SqlServerDataAssemblyMarker).Assembly.GetName().Name));

        return new PunchoutDbContext(builder.Options);
    }
}
