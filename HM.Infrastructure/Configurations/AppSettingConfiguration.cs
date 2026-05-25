using HM.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HM.Infrastructure.Configurations;

public class AppSettingConfiguration : IEntityTypeConfiguration<AppSetting>
{
    public void Configure(EntityTypeBuilder<AppSetting> builder)
    {
        builder.ToTable("AppSettings");
        builder.HasKey(x => x.Key);
        builder.Property(x => x.Key).HasMaxLength(128);
        builder.Property(x => x.Value).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(512);
    }
}
