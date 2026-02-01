using HM.Domain.Entities;
using HM.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HM.Infrastructure.Configurations;

public class TruckConfiguration : IEntityTypeConfiguration<Truck>
{
    public void Configure(EntityTypeBuilder<Truck> builder)
    {
        builder.ToTable("Trucks");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.TruckAccountId)
            .IsRequired();

        builder.Property(t => t.TruckType)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(t => t.MaxWeight)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(t => t.PlateNumber)
            .IsRequired()
            .HasMaxLength(32);

        builder.Property(t => t.IsActive)
            .IsRequired();
    }
}
