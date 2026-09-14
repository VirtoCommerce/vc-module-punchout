using System.Reflection;
using Microsoft.EntityFrameworkCore;
using VirtoCommerce.Platform.Data.Extensions;
using VirtoCommerce.Platform.Data.Infrastructure;
using VirtoCommerce.Punchout.Data.Models;

namespace VirtoCommerce.Punchout.Data.Repositories;

public class PunchoutDbContext : DbContextBase
{
    public PunchoutDbContext(DbContextOptions<PunchoutDbContext> options)
        : base(options)
    {
    }

    protected PunchoutDbContext(DbContextOptions options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<PunchoutSessionEntity>().ToAuditableEntityTable("PunchoutSession");
        modelBuilder.Entity<PunchoutIntegrationEntity>().ToAuditableEntityTable("PunchoutIntegration");

        modelBuilder.Entity<PunchoutIntegrationOrganizationEntity>().ToEntityTable("PunchoutIntegrationOrganization");
        modelBuilder.Entity<PunchoutIntegrationOrganizationEntity>()
            .HasOne(x => x.Integration)
            .WithMany(x => x.Organizations)
            .HasForeignKey(x => x.IntegrationId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<PunchoutIntegrationOrganizationEntity>()
            .HasIndex(x => new { x.OrganizationId, x.IntegrationId })
            .IsUnique()
            .HasDatabaseName("IX_PunchoutIntegrationOrganization_OrganizationId_IntegrationId");

        switch (Database.ProviderName)
        {
            case "Pomelo.EntityFrameworkCore.MySql":
                modelBuilder.ApplyConfigurationsFromAssembly(Assembly.Load("VirtoCommerce.Punchout.Data.MySql"));
                break;
            case "Npgsql.EntityFrameworkCore.PostgreSQL":
                modelBuilder.ApplyConfigurationsFromAssembly(Assembly.Load("VirtoCommerce.Punchout.Data.PostgreSql"));
                break;
            case "Microsoft.EntityFrameworkCore.SqlServer":
                modelBuilder.ApplyConfigurationsFromAssembly(Assembly.Load("VirtoCommerce.Punchout.Data.SqlServer"));
                break;
        }
    }
}
