using HM.Domain.Entities;
using HM.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HM.Infrastructure.Configurations;

public class ShipmentOfferConfiguration : IEntityTypeConfiguration<ShipmentOffer>
{
    public void Configure(EntityTypeBuilder<ShipmentOffer> builder)
    {
        builder.ToTable("ShipmentOffers");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.ShipmentRequestId)
            .IsRequired();

        builder.Property(s => s.TruckAccountId)
            .IsRequired();

        builder.Property(s => s.Price)
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

        builder.Property(s => s.ExpirationAt)
            .IsRequired();
    }
}
