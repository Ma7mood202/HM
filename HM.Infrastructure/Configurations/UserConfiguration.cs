using HM.Domain.Entities;
using HM.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HM.Infrastructure.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    private const int OtpCodeMaxLength = 16;
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.FullName)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(u => u.PhoneNumber)
            .IsRequired()
            .HasMaxLength(32);

        builder.Property(u => u.Email)
            .HasMaxLength(256);

        builder.Property(u => u.IsOtpVerified)
            .IsRequired();

        builder.Property(u => u.OtpCode)
            .HasMaxLength(OtpCodeMaxLength);

        builder.Property(u => u.OtpPurpose)
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(u => u.UserType)
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(u => u.IsActive)
            .IsRequired();

        builder.Property(u => u.CreatedAt)
            .IsRequired();
    }
}
