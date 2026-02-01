using HM.Domain.Entities;
using HM.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HM.Infrastructure.Configurations;

public class ShipmentRequestConfiguration : IEntityTypeConfiguration<ShipmentRequest>
{
    public void Configure(EntityTypeBuilder<ShipmentRequest> builder)
    {
        builder.ToTable("ShipmentRequests");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.MerchantProfileId)
            .IsRequired();

        builder.Property(s => s.PickupLocation)
            .IsRequired()
            .HasMaxLength(512);

        builder.Property(s => s.DropoffLocation)
            .IsRequired()
            .HasMaxLength(512);

        builder.Property(s => s.CargoDescription)
            .IsRequired()
            .HasMaxLength(1024);

        builder.Property(s => s.RequiredTruckType)
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(s => s.EstimatedWeight)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(s => s.Notes)
            .HasMaxLength(1024);

        builder.Property(s => s.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(s => s.CreatedAt)
            .IsRequired();

        builder.HasIndex(s => s.Status);
    }
}
