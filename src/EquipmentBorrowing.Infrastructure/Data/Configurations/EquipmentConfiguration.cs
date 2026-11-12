using EquipmentBorrowing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EquipmentBorrowing.Infrastructure.Data.Configurations;

public class EquipmentConfiguration : IEntityTypeConfiguration<Equipment>
{
    public void Configure(EntityTypeBuilder<Equipment> builder)
    {
        builder.ToTable("Equipment", table =>
        {
            table.HasCheckConstraint("CK_Equipment_EquipmentCode_NotBlank", "btrim(\"EquipmentCode\") <> ''");
            table.HasCheckConstraint("CK_Equipment_Name_NotBlank", "btrim(\"Name\") <> ''");
            table.HasCheckConstraint("CK_Equipment_Description_NotBlank", "\"Description\" IS NULL OR btrim(\"Description\") <> ''");
        });

        builder.HasKey(e => e.Id);

        builder.Property(e => e.EquipmentCode).IsRequired().HasMaxLength(30);
        builder.Property(e => e.Name).IsRequired().HasMaxLength(100);
        builder.Property(e => e.Description).HasMaxLength(500);
        builder.Property(e => e.IsAvailable).IsRequired();

        // BR-05 / BR-16: same technique as the student number.
        builder.Property<string>("NormalizedEquipmentCode")
            .HasComputedColumnSql("lower(btrim(\"EquipmentCode\"))", stored: true);

        builder.HasIndex("NormalizedEquipmentCode")
            .IsUnique()
            .HasDatabaseName("UX_Equipment_NormalizedEquipmentCode");
    }
}