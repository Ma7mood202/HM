using HM.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HM.Infrastructure.Configurations;

public class GovernorateConfiguration : IEntityTypeConfiguration<Governorate>
{
    public void Configure(EntityTypeBuilder<Governorate> builder)
    {
        builder.ToTable("Governorates");
        builder.HasKey(g => g.Id);
        builder.Property(g => g.NameAr).IsRequired().HasMaxLength(128);
        builder.Property(g => g.NameEn).IsRequired().HasMaxLength(128);
        builder.Property(g => g.SortOrder).IsRequired();
    }
}
