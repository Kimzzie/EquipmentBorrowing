using EquipmentBorrowing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EquipmentBorrowing.Infrastructure.Data.Configurations;

public class BorrowingConfiguration : IEntityTypeConfiguration<Borrowing>
{
    public void Configure(EntityTypeBuilder<Borrowing> builder)
    {
        builder.ToTable("Borrowings", table =>
        {
            // BR-11
            table.HasCheckConstraint(
                "CK_Borrowings_ExpectedAfterBorrowed",
                "\"ExpectedReturnDate\" > \"DateBorrowed\"");

            // BR-13: an Active record has no return date; a Returned record has one,
            // and it cannot be earlier than the checkout date.
            table.HasCheckConstraint(
                "CK_Borrowings_StatusDates",
                "(\"Status\" = 'Active' AND \"DateReturned\" IS NULL) OR " +
                "(\"Status\" = 'Returned' AND \"DateReturned\" IS NOT NULL AND \"DateReturned\" >= \"DateBorrowed\")");
        });

        builder.HasKey(b => b.Id);

        builder.Property(b => b.Status)
            .IsRequired()
            .HasMaxLength(20)
            .HasConversion<string>();   // stored as text: "Active" / "Returned"

        builder.Property(b => b.DateBorrowed).IsRequired();
        builder.Property(b => b.ExpectedReturnDate).IsRequired();
        // DateReturned is nullable: no .IsRequired().

        // BR-14: enforced foreign keys. Restrict = a student or item that has
        // history can never be deleted out from under it.
        builder.HasOne<Student>()
            .WithMany()
            .HasForeignKey(b => b.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Equipment>()
            .WithMany()
            .HasForeignKey(b => b.EquipmentId)
            .OnDelete(DeleteBehavior.Restrict);

        // Speeds up "active count for a student" and student history.
        builder.HasIndex(b => new { b.StudentId, b.Status })
            .HasDatabaseName("IX_Borrowings_StudentId_Status");

        // Speeds up equipment history (also covers the EquipmentId foreign key).
        builder.HasIndex(b => new { b.EquipmentId, b.DateBorrowed })
            .HasDatabaseName("IX_Borrowings_EquipmentId_DateBorrowed");

        // BR-10 / BR-16: at most ONE active borrowing per item, enforced by PostgreSQL
        // as a partial unique index. Returned rows are ignored, so an item can have
        // unlimited history but only one current holder.
        builder.HasIndex(b => b.EquipmentId)
            .IsUnique()
            .HasFilter("\"Status\" = 'Active'")
            .HasDatabaseName("UX_Borrowings_OneActivePerEquipment");
    }
}