using HM.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HM.Infrastructure.Configurations;

public class DriverProfileConfiguration : IEntityTypeConfiguration<DriverProfile>
{
    public void Configure(EntityTypeBuilder<DriverProfile> builder)
    {
        builder.ToTable("DriverProfiles");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.FullName)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(d => d.NationalIdImageUrl)
            .HasMaxLength(1024);

        builder.Property(d => d.IsVerified)
            .IsRequired();

        builder.Property(d => d.CreatedAt)
            .IsRequired();
    }
}
