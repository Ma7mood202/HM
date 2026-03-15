using HM.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HM.Infrastructure.Configurations;

public class RegionConfiguration : IEntityTypeConfiguration<Region>
{
    public void Configure(EntityTypeBuilder<Region> builder)
    {
        builder.ToTable("Regions");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.GovernorateId).IsRequired();
        builder.Property(r => r.NameAr).IsRequired().HasMaxLength(128);
        builder.Property(r => r.NameEn).IsRequired().HasMaxLength(128);
        builder.Property(r => r.SortOrder).IsRequired();

        builder.HasOne(r => r.Governorate)
            .WithMany()
            .HasForeignKey(r => r.GovernorateId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
