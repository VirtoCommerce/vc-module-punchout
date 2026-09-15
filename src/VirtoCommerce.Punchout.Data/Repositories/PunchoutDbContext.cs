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
        // The session token is the correlation key the storefront resolves a session by.
        modelBuilder.Entity<PunchoutSessionEntity>()
            .HasIndex(x => x.SessionToken)
            .IsUnique()
            .HasDatabaseName("IX_PunchoutSession_SessionToken");
        modelBuilder.Entity<PunchoutIntegrationEntity>().ToAuditableEntityTable("PunchoutIntegration");
        // Integrations are looked up by organization from the organization details widget.
        modelBuilder.Entity<PunchoutIntegrationEntity>()
            .HasIndex(x => x.OrganizationId)
            .HasDatabaseName("IX_PunchoutIntegration_OrganizationId");

        modelBuilder.Entity<PunchoutUserMappingEntity>().ToAuditableEntityTable("PunchoutUserMapping");
        // The sender identity is the only thing a setup request presents to say who is punching out,
        // so it must resolve to exactly one platform user.
        modelBuilder.Entity<PunchoutUserMappingEntity>()
            .HasIndex(x => x.ExternalId)
            .IsUnique()
            .HasDatabaseName("IX_PunchoutUserMapping_ExternalId");
        // Mappings are looked up by member from the contact details widget.
        modelBuilder.Entity<PunchoutUserMappingEntity>()
            .HasIndex(x => x.MemberId)
            .HasDatabaseName("IX_PunchoutUserMapping_MemberId");
        modelBuilder.Entity<PunchoutUserMappingEntity>()
            .HasIndex(x => x.UserId)
            .HasDatabaseName("IX_PunchoutUserMapping_UserId");

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
