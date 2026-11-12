using EquipmentBorrowing.Domain;
using Microsoft.EntityFrameworkCore;

namespace EquipmentBorrowing.Infrastructure.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(
        EquipmentBorrowingDbContext context,
        CancellationToken cancellationToken = default)
    {
        // Guard: seed ONLY a completely empty database. If staff already registered
        // anything, we never add (or overwrite) rows on startup. Checking all three
        // tables, not just Students, covers a database that has equipment but no students.
        var hasData = await context.Students.AnyAsync(cancellationToken)
                      || await context.Equipment.AnyAsync(cancellationToken)
                      || await context.Borrowings.AnyAsync(cancellationToken);
        if (hasData)
        {
            return;
        }

        context.Students.AddRange(
            new Student(id: 0, studentNumber: "2023-0001", name: "Bryl Lim", isAllowedToBorrow: true, contact: "09170000001"),
            new Student(id: 0, studentNumber: "2023-0002", name: "Boss Rod", isAllowedToBorrow: false),   // ineligible, for rejection demos
            new Student(id: 0, studentNumber: "2023-0003", name: "Kim Cabatingan", isAllowedToBorrow: true),
            new Student(id: 0, studentNumber: "2023-0004", name: "Ana Reyes", isAllowedToBorrow: true, contact: "09170000004"));

        context.Equipment.AddRange(
            new Equipment(id: 0, equipmentCode: "EQ-001", name: "Crimping Tool", description: "Networking Tool"),
            new Equipment(id: 0, equipmentCode: "EQ-002", name: "LAN Tester", description: "Networking Tool"),
            new Equipment(id: 0, equipmentCode: "EQ-003", name: "Aircon Remote", description: "Remote Control"),
            new Equipment(id: 0, equipmentCode: "EQ-004", name: "Projector", description: "AV Equipment"),
            new Equipment(id: 0, equipmentCode: "EQ-005", name: "Digital Multimeter", description: "Electronics"),
            new Equipment(id: 0, equipmentCode: "EQ-006", name: "HDMI Cable"));                          // no description: optional

        // One SaveChanges = one transaction: all seed rows are inserted, or none.
        await context.SaveChangesAsync(cancellationToken);
    }
}