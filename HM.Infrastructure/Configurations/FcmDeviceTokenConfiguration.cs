using HM.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HM.Infrastructure.Configurations;

public class FcmDeviceTokenConfiguration : IEntityTypeConfiguration<FcmDeviceToken>
{
    public void Configure(EntityTypeBuilder<FcmDeviceToken> builder)
    {
        builder.ToTable("FcmDeviceTokens");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Token)
            .IsRequired()
            .HasMaxLength(512);

        builder.Property(t => t.Platform)
            .IsRequired()
            .HasMaxLength(32);

        builder.Property(t => t.CreatedAt)
            .IsRequired();

        builder.HasOne(t => t.User)
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(t => t.UserId);
        builder.HasIndex(t => new { t.UserId, t.Token }).IsUnique();
    }
}
