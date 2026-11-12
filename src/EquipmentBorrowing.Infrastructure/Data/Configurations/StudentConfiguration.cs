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
            table.HasCheckConstraint("CK_Students_StudentNumber_NotBlank", "btrim(\"StudentNumber\") <> ''");
            table.HasCheckConstraint("CK_Students_Name_NotBlank", "btrim(\"Name\") <> ''");
            table.HasCheckConstraint("CK_Students_Contact_NotBlank", "\"Contact\" IS NULL OR btrim(\"Contact\") <> ''");
        });

        builder.HasKey(s => s.Id);

        builder.Property(s => s.StudentNumber).IsRequired().HasMaxLength(Student.StudentNumberMaxLength);
        builder.Property(s => s.Name).IsRequired().HasMaxLength(Student.NameMaxLength);
        builder.Property(s => s.Contact).HasMaxLength(Student.ContactMaxLength);
        builder.Property(s => s.IsAllowedToBorrow).IsRequired();

        // BR-01 / BR-16: unique after trimming and ignoring case, computed by PostgreSQL.
        builder.Property<string>("NormalizedStudentNumber")
            .HasComputedColumnSql("lower(btrim(\"StudentNumber\"))", stored: true);

        builder.HasIndex("NormalizedStudentNumber")
            .IsUnique()
            .HasDatabaseName("UX_Students_NormalizedStudentNumber");
    }
}