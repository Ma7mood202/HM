using HM.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HM.Infrastructure.Configurations;

public class AdminAuditLogConfiguration : IEntityTypeConfiguration<AdminAuditLog>
{
    public void Configure(EntityTypeBuilder<AdminAuditLog> builder)
    {
        builder.ToTable("AdminAuditLogs");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.AdminEmail).IsRequired().HasMaxLength(256);
        builder.Property(x => x.Action).IsRequired().HasMaxLength(128);
        builder.Property(x => x.EntityType).IsRequired().HasMaxLength(128);
        builder.Property(x => x.EntityId).HasMaxLength(128);
        builder.Property(x => x.IpAddress).HasMaxLength(64);

        builder.HasIndex(x => x.AdminUserId);
        builder.HasIndex(x => x.CreatedAt);
        builder.HasIndex(x => new { x.EntityType, x.EntityId });
    }
}
