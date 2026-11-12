using EquipmentBorrowing.Domain;
using Microsoft.EntityFrameworkCore;

namespace EquipmentBorrowing.Infrastructure.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(
        EquipmentBorrowingDbContext context,
        CancellationToken cancellationToken = default)
    {
        // Guard: only seed on a truly empty database.
        if (await context.Students.AnyAsync(cancellationToken))
        {
            return;
        }

        context.Students.AddRange(
            new Student(id: 0, studentNumber: "2023-0001", name: "Bryl Lim", isAllowedToBorrow: true),
            new Student(id: 0, studentNumber: "2023-0002", name: "Boss Rod", isAllowedToBorrow: false),
            new Student(id: 0, studentNumber: "2023-0003", name: "Kim Cabatingan", isAllowedToBorrow: true));

        context.Equipment.AddRange(
            new Equipment(id: 0, equipmentCode: "EQ-001", name: "Crimping Tool", description: "Networking Tool"),
            new Equipment(id: 0, equipmentCode: "EQ-002", name: "LAN Tester", description: "Networking Tool"),
            new Equipment(id: 0, equipmentCode: "EQ-003", name: "Aircon Remote", description: "Remote Control"),
            new Equipment(id: 0, equipmentCode: "EQ-004", name: "Projector", description: "AV Equipment"));

        await context.SaveChangesAsync(cancellationToken);
    }
}