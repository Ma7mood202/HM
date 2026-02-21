using HM.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HM.Infrastructure.Configurations;

public class TruckAccountConfiguration : IEntityTypeConfiguration<TruckAccount>
{
    public void Configure(EntityTypeBuilder<TruckAccount> builder)
    {
        builder.ToTable("TruckAccounts");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.UserId)
            .IsRequired();

        builder.Property(t => t.IsAvailable)
            .IsRequired();

        builder.Property(t => t.CreatedAt)
            .IsRequired();

        builder.Property(t => t.FullName)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(t => t.AvatarUrl)
            .HasMaxLength(1024);

        builder.Property(t => t.NationalIdFrontImageUrl)
            .HasMaxLength(1024);

        builder.Property(t => t.NationalIdBackImageUrl)
            .HasMaxLength(1024);

        builder.Property(t => t.IsVerified)
            .IsRequired();
    }
}
