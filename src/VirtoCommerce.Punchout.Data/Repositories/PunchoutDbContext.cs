using System.Reflection;
using Microsoft.EntityFrameworkCore;
using VirtoCommerce.Platform.Data.Extensions;
using VirtoCommerce.Platform.Data.Infrastructure;
using VirtoCommerce.Punchout.Data.Models;

namespace VirtoCommerce.Punchout.Data.Repositories;

public class PunchoutDbContext : DbContextBase
{
    public const int DecimalPrecision = 18;
    public const int DecimalScale = 4;

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
        modelBuilder.Entity<PunchoutSessionEntity>()
            .HasIndex(x => x.SessionTokenHash)
            .IsUnique()
            .HasDatabaseName("IX_PunchoutSession_SessionToken");

        modelBuilder.Entity<PunchoutUserMappingEntity>().ToAuditableEntityTable("PunchoutUserMapping");
        modelBuilder.Entity<PunchoutUserMappingEntity>()
            .HasIndex(x => x.ExternalId)
            .HasDatabaseName("IX_PunchoutUserMapping_ExternalId");
        modelBuilder.Entity<PunchoutUserMappingEntity>()
            .HasIndex(x => x.MemberId)
            .HasDatabaseName("IX_PunchoutUserMapping_MemberId");
        modelBuilder.Entity<PunchoutUserMappingEntity>()
            .HasIndex(x => x.UserId)
            .HasDatabaseName("IX_PunchoutUserMapping_UserId");
        modelBuilder.Entity<PunchoutUserMappingEntity>()
            .HasIndex(x => x.OrganizationId)
            .HasDatabaseName("IX_PunchoutUserMapping_OrganizationId");

        modelBuilder.Entity<PunchoutOrderMessageEntity>().ToAuditableEntityTable("PunchoutOrderMessage");
        modelBuilder.Entity<PunchoutOrderMessageEntity>()
            .HasOne<PunchoutSessionEntity>()
            .WithMany()
            .HasForeignKey(x => x.SessionId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<PunchoutOrderMessageEntity>()
            .HasIndex(x => x.SessionId)
            .HasDatabaseName("IX_PunchoutOrderMessage_SessionId");

        modelBuilder.Entity<PunchoutRequisitionItemEntity>().ToEntityTable("PunchoutRequisitionItem");
        modelBuilder.Entity<PunchoutRequisitionItemEntity>()
            .HasOne(x => x.OrderMessage)
            .WithMany(x => x.Items)
            .HasForeignKey(x => x.OrderMessageId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<PunchoutRequisitionItemEntity>()
            .HasIndex(x => new { x.OrderMessageId, x.LineItemId })
            .IsUnique()
            .HasDatabaseName("IX_PunchoutRequisitionItem_OrderMessageId_LineItemId");
        modelBuilder.Entity<PunchoutRequisitionItemEntity>()
            .HasIndex(x => x.LineItemId)
            .HasDatabaseName("IX_PunchoutRequisitionItem_LineItemId");

        modelBuilder.Entity<PunchoutOrderRequestEntity>().ToAuditableEntityTable("PunchoutOrderRequest");
        modelBuilder.Entity<PunchoutOrderRequestEntity>()
            .HasIndex(x => new { x.ConfigurationId, x.PayloadId })
            .IsUnique()
            .HasDatabaseName("IX_PunchoutOrderRequest_ConfigurationId_PayloadId");
        modelBuilder.Entity<PunchoutOrderRequestEntity>()
            .HasIndex(x => new { x.ConfigurationId, x.PurchaseOrderNumber })
            .HasDatabaseName("IX_PunchoutOrderRequest_ConfigurationId_PurchaseOrderNumber");
        modelBuilder.Entity<PunchoutOrderRequestEntity>()
            .HasIndex(x => x.CustomerOrderId)
            .HasDatabaseName("IX_PunchoutOrderRequest_CustomerOrderId");

        // A requisition item is ordered by one purchase order only
        modelBuilder.Entity<PunchoutOrderLineEntity>().ToAuditableEntityTable("PunchoutOrderLine");
        modelBuilder.Entity<PunchoutOrderLineEntity>()
            .HasIndex(x => new { x.ConfigurationId, x.SessionId, x.LineItemId })
            .IsUnique()
            .HasDatabaseName("IX_PunchoutOrderLine_ConfigurationId_SessionId_LineItemId");
        modelBuilder.Entity<PunchoutOrderLineEntity>()
            .HasIndex(x => new { x.ConfigurationId, x.LineItemId })
            .HasDatabaseName("IX_PunchoutOrderLine_ConfigurationId_LineItemId");
        modelBuilder.Entity<PunchoutOrderLineEntity>()
            .HasIndex(x => new { x.ConfigurationId, x.PurchaseOrderNumber })
            .HasDatabaseName("IX_PunchoutOrderLine_ConfigurationId_PurchaseOrderNumber");

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
