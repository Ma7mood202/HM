using HM.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HM.Infrastructure.Configurations;

public class DriverInvitationConfiguration : IEntityTypeConfiguration<DriverInvitation>
{
    public void Configure(EntityTypeBuilder<DriverInvitation> builder)
    {
        builder.ToTable("DriverInvitations");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.ShipmentId)
            .IsRequired();

        builder.Property(d => d.Token)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(d => d.ExpiresAt)
            .IsRequired();

        builder.Property(d => d.IsUsed)
            .IsRequired();

        builder.Property(d => d.CreatedAt)
            .IsRequired();
    }
}
