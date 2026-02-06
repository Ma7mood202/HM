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

        builder.Property(s => s.RequestNumber)
            .IsRequired()
            .HasMaxLength(32);
        builder.HasIndex(s => s.RequestNumber)
            .IsUnique();

        builder.Property(s => s.RequiredTruckType)
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(s => s.PickupLocation)
            .IsRequired()
            .HasMaxLength(512);
        builder.Property(s => s.PickupArea)
            .HasMaxLength(256);
        builder.Property(s => s.PickupLat);
        builder.Property(s => s.PickupLng);

        builder.Property(s => s.DropoffLocation)
            .IsRequired()
            .HasMaxLength(512);
        builder.Property(s => s.DropoffArea)
            .HasMaxLength(256);
        builder.Property(s => s.DropoffLat);
        builder.Property(s => s.DropoffLng);

        builder.Property(s => s.SenderName)
            .IsRequired()
            .HasMaxLength(256);
        builder.Property(s => s.SenderPhone)
            .IsRequired()
            .HasMaxLength(32);

        builder.Property(s => s.CargoDescription)
            .IsRequired()
            .HasMaxLength(1024);
        builder.Property(s => s.ParcelType)
            .HasMaxLength(128);
        builder.Property(s => s.EstimatedWeight)
            .HasPrecision(18, 4)
            .IsRequired();
        builder.Property(s => s.ParcelSize)
            .HasMaxLength(64);
        builder.Property(s => s.ParcelCount)
            .IsRequired();

        builder.Property(s => s.DeliveryDate);
        builder.Property(s => s.DeliveryTimeFrom);
        builder.Property(s => s.DeliveryTimeTo);

        builder.Property(s => s.PaymentMethod)
            .HasConversion<string>()
            .HasMaxLength(32)
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
        builder.HasIndex(s => s.MerchantProfileId);
    }
}
