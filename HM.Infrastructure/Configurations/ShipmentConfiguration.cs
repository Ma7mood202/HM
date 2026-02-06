using HM.Domain.Entities;
using HM.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HM.Infrastructure.Configurations;

public class ShipmentConfiguration : IEntityTypeConfiguration<Shipment>
{
    public void Configure(EntityTypeBuilder<Shipment> builder)
    {
        builder.ToTable("Shipments");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.ShipmentRequestId)
            .IsRequired();

        builder.Property(s => s.AcceptedOfferId)
            .IsRequired();

        builder.Property(s => s.TruckId)
            .IsRequired();

        builder.Property(s => s.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(s => s.StartedAt);
        builder.Property(s => s.CompletedAt);
        builder.Property(s => s.CurrentLat);
        builder.Property(s => s.CurrentLng);
        builder.Property(s => s.LocationUpdatedAt);
    }
}
