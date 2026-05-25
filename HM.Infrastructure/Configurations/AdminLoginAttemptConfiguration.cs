using HM.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HM.Infrastructure.Configurations;

public class AdminLoginAttemptConfiguration : IEntityTypeConfiguration<AdminLoginAttempt>
{
    public void Configure(EntityTypeBuilder<AdminLoginAttempt> builder)
    {
        builder.ToTable("AdminLoginAttempts");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Email).IsRequired().HasMaxLength(256);
        builder.Property(x => x.IpAddress).HasMaxLength(64);
        builder.Property(x => x.UserAgent).HasMaxLength(512);

        builder.HasIndex(x => x.Email);
        builder.HasIndex(x => x.CreatedAt);
    }
}
