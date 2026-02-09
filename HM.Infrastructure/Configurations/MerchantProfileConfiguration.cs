using HM.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HM.Infrastructure.Configurations;

public class MerchantProfileConfiguration : IEntityTypeConfiguration<MerchantProfile>
{
    public void Configure(EntityTypeBuilder<MerchantProfile> builder)
    {
        builder.ToTable("MerchantProfiles");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.UserId)
            .IsRequired();

        builder.Property(m => m.AvatarUrl)
            .HasMaxLength(512);

        builder.Property(m => m.IsVerified)
            .IsRequired();

        builder.Property(m => m.CreatedAt)
            .IsRequired();
    }
}
