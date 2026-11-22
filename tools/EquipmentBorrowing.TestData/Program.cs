using EquipmentBorrowing.Domain;
using EquipmentBorrowing.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

// Acceptance-test data tool for Lab 5 (reference Section 3).
//   reset    : applies migrations, wipes ALL rows, inserts S1-S4 / E1-E7 (only on a *_test database)
//   snapshot : prints the three tables plus a consistency check, using raw SQL (read-only)

var command = args.Length == 1 ? args[0].ToLowerInvariant() : string.Empty;
if (command is not ("reset" or "snapshot"))
{
    Console.WriteLine("Usage: dotnet run --project tools\\EquipmentBorrowing.TestData -- reset|snapshot");
    return 1;
}

NpgsqlConnectionStringBuilder target;
string connectionString;
try
{
    // Same lookup as the API: EQUIPMENT_BORROWING_CONNECTION first, then appsettings.Local.json.
    connectionString = DatabaseConnection.GetConnectionString();
    target = new NpgsqlConnectionStringBuilder(connectionString);
}
catch (Exception ex) when (ex is DatabaseConfigurationException or ArgumentException)
{
    Console.WriteLine($"REFUSED: {ex.Message}");
    return 1;
}

if (command == "snapshot")
    return await SnapshotAsync(connectionString, target);

// ---------------- reset ----------------

// SAFETY: this tool deletes every row. Never allow it on a real database.
if (target.Database is null || !target.Database.EndsWith("_test", StringComparison.OrdinalIgnoreCase))
{
    Console.WriteLine(
        $"REFUSED: the connected database is '{target.Database}', which does not end in '_test'. " +
        "This tool wipes all data, so it only runs on a disposable test database. Nothing was changed.");
    return 2;
}

var options = new DbContextOptionsBuilder<EquipmentBorrowingDbContext>()
    .UseNpgsql(connectionString)
    .Options;

await using var context = new EquipmentBorrowingDbContext(options);

try
{
    Console.WriteLine($"Target: database '{target.Database}' on {target.Host}:{target.Port}");

    // 1) Schema: applies only the migrations this database does not have yet.
    await context.Database.MigrateAsync();

    // 2) Wipe. One statement, so the foreign keys between the three tables are no problem.
    //    RESTART IDENTITY makes ids start at 1 again, so evidence looks the same every time.
    await context.Database.ExecuteSqlRawAsync(
        "TRUNCATE TABLE \"Borrowings\", \"Equipment\", \"Students\" RESTART IDENTITY");

    // 3) Students: S1-S4 (S2 is the ineligible one).
    var s1 = new Student(0, "ST-100", "Ada Cruz");
    var s2 = new Student(0, "ST-200", "Ben Diaz", isAllowedToBorrow: false);
    var s3 = new Student(0, "ST-300", "Cara Lim");
    var s4 = new Student(0, "ST-400", "Dani Ong");

    // 4) Equipment: E1, E4 and E5 start unavailable because they are on loan.
    var e1 = new Equipment(0, "EQ-001", "Camera", isAvailable: false);
    var e2 = new Equipment(0, "EQ-002", "Laptop");
    var e3 = new Equipment(0, "EQ-003", "Projector");
    var e4 = new Equipment(0, "EQ-004", "Tablet", isAvailable: false);
    var e5 = new Equipment(0, "EQ-005", "Microphone", isAvailable: false);
    var e6 = new Equipment(0, "EQ-006", "Tripod");
    var e7 = new Equipment(0, "EQ-007", "Speaker");

    context.Students.AddRange(s1, s2, s3, s4);
    context.Equipment.AddRange(e1, e2, e3, e4, e5, e6, e7);
    await context.SaveChangesAsync();   // ids are generated and written back to the objects

    // 5) Borrowings need those ids, so they are saved second.
    var b1 = new Borrowing(0, s1.Id, e1.Id, new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 9));   // S1 has E1, due 11-09
    var b2 = new Borrowing(0, s1.Id, e3.Id, new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 12));  // S1 returned E3 on 11-08
    b2.MarkAsReturned(new DateOnly(2026, 11, 8));
    var b3 = new Borrowing(0, s3.Id, e4.Id, new DateOnly(2026, 11, 3), new DateOnly(2026, 11, 12));  // S3 has E4
    var b4 = new Borrowing(0, s3.Id, e5.Id, new DateOnly(2026, 11, 3), new DateOnly(2026, 11, 12));  // S3 has E5

    context.Borrowings.AddRange(b1, b2, b3, b4);
    await context.SaveChangesAsync();

    Console.WriteLine(
        $"Reset complete: {await context.Students.CountAsync()} students, " +
        $"{await context.Equipment.CountAsync()} equipment, {await context.Borrowings.CountAsync()} borrowings " +
        $"({await context.Borrowings.CountAsync(b => b.Status == BorrowingStatus.Active)} active).");
    Console.WriteLine("Run the API with the test clock (2026-11-10) before testing.");
    return 0;
}
catch (Exception ex) when (ex is NpgsqlException or DbUpdateException or InvalidOperationException)
{
    // GetBaseException() = the real cause (e.g. "database does not exist"); no connection string is printed.
    Console.WriteLine($"FAILED: {ex.GetBaseException().Message}");
    return 3;
}

// ---------------- snapshot ----------------

// Plain SQL through Npgsql: no EF, no API. Output has no timestamps, so two snapshots of the
// same data are byte-for-byte identical and can be compared by the test script.
static async Task<int> SnapshotAsync(string connectionString, NpgsqlConnectionStringBuilder target)
{
    try
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        Console.WriteLine($"Database: {target.Database}");
        Console.WriteLine();

        await PrintAsync(connection, "Students", """
            SELECT "Id", "StudentNumber", "Name", "Contact", "IsAllowedToBorrow"
            FROM "Students" ORDER BY "Id"
            """);

        await PrintAsync(connection, "Equipment", """
            SELECT "Id", "EquipmentCode", "Name", "Description", "IsAvailable"
            FROM "Equipment" ORDER BY "Id"
            """);

        await PrintAsync(connection, "Borrowings", """
            SELECT b."Id", s."StudentNumber", e."EquipmentCode",
                   b."DateBorrowed"::text AS "DateBorrowed",
                   b."ExpectedReturnDate"::text AS "ExpectedReturnDate",
                   b."DateReturned"::text AS "DateReturned",
                   b."Status"
            FROM "Borrowings" b
            JOIN "Students"  s ON s."Id" = b."StudentId"
            JOIN "Equipment" e ON e."Id" = b."EquipmentId"
            ORDER BY b."Id"
            """);

        // An item is available exactly when it has no active borrowing.
        // Any row printed here means a partial write.
        await PrintAsync(connection, "Consistency check (must be 0 rows)", """
            SELECT e."EquipmentCode", e."IsAvailable",
                   COUNT(b."Id") FILTER (WHERE b."Status" = 'Active') AS "ActiveBorrowings"
            FROM "Equipment" e
            LEFT JOIN "Borrowings" b ON b."EquipmentId" = e."Id"
            GROUP BY e."Id", e."EquipmentCode", e."IsAvailable"
            HAVING e."IsAvailable" = (COUNT(b."Id") FILTER (WHERE b."Status" = 'Active') > 0)
            ORDER BY e."Id"
            """);

        return 0;
    }
    catch (NpgsqlException ex)
    {
        Console.WriteLine($"FAILED: {ex.GetBaseException().Message}");
        return 3;
    }
}

static async Task PrintAsync(NpgsqlConnection connection, string title, string sql)
{
    await using var command = new NpgsqlCommand(sql, connection);
    await using var reader = await command.ExecuteReaderAsync();

    Console.WriteLine($"-- {title}");
    Console.WriteLine(string.Join(" | ", Enumerable.Range(0, reader.FieldCount).Select(reader.GetName)));

    var rows = 0;
    while (await reader.ReadAsync())
    {
        var cells = new string[reader.FieldCount];
        for (var i = 0; i < reader.FieldCount; i++)
            cells[i] = reader.IsDBNull(i) ? "NULL" : Convert.ToString(reader.GetValue(i)) ?? string.Empty;

        Console.WriteLine(string.Join(" | ", cells));
        rows++;
    }

    Console.WriteLine($"({rows} rows)");
    Console.WriteLine();
}
