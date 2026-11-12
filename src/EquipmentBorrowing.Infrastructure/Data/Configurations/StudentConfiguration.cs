using EquipmentBorrowing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EquipmentBorrowing.Infrastructure.Data.Configurations;

public class StudentConfiguration : IEntityTypeConfiguration<Student>
{
    public void Configure(EntityTypeBuilder<Student> builder)
    {
        builder.ToTable("Students", table =>
        {
            // The domain already trims and rejects blanks; these make the DATABASE
            // refuse them too, even for a row inserted by hand (BR-01, BR-02, BR-16).
            table.HasCheckConstraint("CK_Students_StudentNumber_NotBlank", "btrim(\"StudentNumber\") <> ''");
            table.HasCheckConstraint("CK_Students_Name_NotBlank", "btrim(\"Name\") <> ''");
            table.HasCheckConstraint("CK_Students_Contact_NotBlank", "\"Contact\" IS NULL OR btrim(\"Contact\") <> ''");
        });

        builder.HasKey(s => s.Id);

        builder.Property(s => s.StudentNumber).IsRequired().HasMaxLength(20);
        builder.Property(s => s.Name).IsRequired().HasMaxLength(100);
        builder.Property(s => s.Contact).HasMaxLength(100);
        builder.Property(s => s.IsAllowedToBorrow).IsRequired();

        // BR-01 / BR-16: unique AFTER trimming and ignoring case. PostgreSQL itself
        // computes this column, so the normalization rule and the uniqueness rule can
        // never drift apart. It is a shadow property: the C# class does not have it.
        builder.Property<string>("NormalizedStudentNumber")
            .HasComputedColumnSql("lower(btrim(\"StudentNumber\"))", stored: true);

        builder.HasIndex("NormalizedStudentNumber")
            .IsUnique()
            .HasDatabaseName("UX_Students_NormalizedStudentNumber");
    }
}