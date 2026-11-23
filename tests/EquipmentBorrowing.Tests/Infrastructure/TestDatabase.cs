using System.Text;
using EquipmentBorrowing.Domain;
using EquipmentBorrowing.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EquipmentBorrowing.Tests.Infrastructure;

/// <summary>
/// Everything the tests need from PostgreSQL, in one place:
///  - a safety guard (never touch a database that is not a *_test database),
///  - ResetAsync(): the Lab 6 fixture S1-S4 / E1-E7 relative to the fixed date D,
///  - fresh-connection readback helpers for "did it really persist / roll back?" assertions.
/// </summary>
public static class TestDatabase
{
    public const string ConnectionVariable = "EQUIPMENT_BORROWING_CONNECTION";

    /// <summary>The documented fixed local calendar date D for every test (Lab 6 suite).</summary>
    public static readonly DateOnly D = new(2026, 11, 10);

    private static bool _migrated;

    /// <summary>
    /// Deliberately stricter than the app: it reads ONLY the environment variable, never
    /// appsettings.Local.json (which might point at the real database), and it refuses any
    /// database whose name does not end in "_test".
    /// </summary>
    public static string ConnectionString
    {
        get
        {
            var value = Environment.GetEnvironmentVariable(ConnectionVariable);
            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidOperationException(
                    $"Set the {ConnectionVariable} environment variable to your *_test database before running tests. " +
                    "The tests never fall back to appsettings.Local.json.");

            var database = new NpgsqlConnectionStringBuilder(value).Database;
            if (database is null || !database.EndsWith("_test", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    $"REFUSED: the tests would run against '{database}', which does not end in '_test'. " +
                    "Tests wipe all data. Nothing was changed.");

            return value;
        }
    }

    // ---------------- reset ----------------

    /// <summary>Wipes the three tables and inserts the Lab 6 dataset. Call before EVERY test.</summary>
    public static async Task ResetAsync()
    {
        var options = new DbContextOptionsBuilder<EquipmentBorrowingDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        await using var context = new EquipmentBorrowingDbContext(options);

        if (!_migrated)
        {
            await context.Database.MigrateAsync();   // only applies migrations that are missing
            _migrated = true;
        }

        await context.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE \"Borrowings\", \"Equipment\", \"Students\" RESTART IDENTITY");

        var s1 = new Student(0, "ST-100", "Ada Cruz");
        var s2 = new Student(0, "ST-200", "Ben Diaz", isAllowedToBorrow: false);
        var s3 = new Student(0, "ST-300", "Cara Lim");
        var s4 = new Student(0, "ST-400", "Dani Ong");

        var e1 = new Equipment(0, "EQ-001", "Camera", isAvailable: false);
        var e2 = new Equipment(0, "EQ-002", "Laptop");
        var e3 = new Equipment(0, "EQ-003", "Projector");
        var e4 = new Equipment(0, "EQ-004", "Tablet", isAvailable: false);
        var e5 = new Equipment(0, "EQ-005", "Microphone", isAvailable: false);
        var e6 = new Equipment(0, "EQ-006", "Tripod");
        var e7 = new Equipment(0, "EQ-007", "Speaker");

        context.Students.AddRange(s1, s2, s3, s4);
        context.Equipment.AddRange(e1, e2, e3, e4, e5, e6, e7);
        await context.SaveChangesAsync();

        // Dates are written relative to D exactly as in the Lab 6 suite.
        var b1 = new Borrowing(0, s1.Id, e1.Id, D.AddDays(-9), D.AddDays(-1));   // S1 active E1 (overdue)
        var b2 = new Borrowing(0, s1.Id, e3.Id, D.AddDays(-8), D.AddDays(2));    // S1 returned E3
        b2.MarkAsReturned(D.AddDays(-2));
        var b3 = new Borrowing(0, s3.Id, e4.Id, D.AddDays(-7), D.AddDays(2));    // S3 active E4
        var b4 = new Borrowing(0, s3.Id, e5.Id, D.AddDays(-7), D.AddDays(2));    // S3 active E5

        context.Borrowings.AddRange(b1, b2, b3, b4);
        await context.SaveChangesAsync();
    }

    // ---------------- fresh-connection readback ----------------

    /// <summary>
    /// A brand-new PHYSICAL connection (pooling off) that shares nothing with the API's DbContext.
    /// This is what makes a readback "fresh and independent".
    /// </summary>
    public static async Task<NpgsqlConnection> OpenFreshConnectionAsync()
    {
        var builder = new NpgsqlConnectionStringBuilder(ConnectionString) { Pooling = false };
        var connection = new NpgsqlConnection(builder.ConnectionString);
        await connection.OpenAsync();
        return connection;
    }

    public static async Task<T> ScalarAsync<T>(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = await OpenFreshConnectionAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);

        var result = await command.ExecuteScalarAsync();
        return (T)Convert.ChangeType(result!, typeof(T));
    }

    /// <summary>Runs one non-query statement on a fresh connection (test setup only).</summary>
    public static async Task ExecuteAsync(string sql)
    {
        await using var connection = await OpenFreshConnectionAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>Empties all three tables so a test can check the "empty catalog" state.</summary>
    public static Task ClearAllAsync() =>
        ExecuteAsync("TRUNCATE TABLE \"Borrowings\", \"Equipment\", \"Students\" RESTART IDENTITY");

    public static Task<long> CountAsync(string table) =>
        ScalarAsync<long>($"SELECT COUNT(*) FROM \"{table}\"");

    // Targeted fresh-connection readbacks (each opens its own new connection).

    public static Task<bool> IsAvailableAsync(int equipmentId) => ScalarAsync<bool>(
        "SELECT \"IsAvailable\" FROM \"Equipment\" WHERE \"Id\" = @id", ("id", equipmentId));

    public static Task<bool> IsAllowedToBorrowAsync(int studentId) => ScalarAsync<bool>(
        "SELECT \"IsAllowedToBorrow\" FROM \"Students\" WHERE \"Id\" = @id", ("id", studentId));

    public static Task<string> BorrowingStatusAsync(int borrowingId) => ScalarAsync<string>(
        "SELECT \"Status\" FROM \"Borrowings\" WHERE \"Id\" = @id", ("id", borrowingId));

    /// <summary>The stored return date as yyyy-MM-dd text (only call for a returned borrowing).</summary>
    public static Task<string> ReturnedDateAsync(int borrowingId) => ScalarAsync<string>(
        "SELECT to_char(\"DateReturned\", 'YYYY-MM-DD') FROM \"Borrowings\" WHERE \"Id\" = @id", ("id", borrowingId));

    public static Task<long> BorrowingCountForStudentAsync(int studentId) => ScalarAsync<long>(
        "SELECT COUNT(*) FROM \"Borrowings\" WHERE \"StudentId\" = @id", ("id", studentId));

    public static Task<long> ActiveCountForStudentAsync(int studentId) => ScalarAsync<long>(
        "SELECT COUNT(*) FROM \"Borrowings\" WHERE \"StudentId\" = @id AND \"Status\" = 'Active'", ("id", studentId));

    /// <summary>Number of items whose availability disagrees with their active borrowings. Must be 0.</summary>
    public static Task<long> ConsistencyViolationsAsync() => ScalarAsync<long>("""
        SELECT COUNT(*) FROM (
            SELECT e."Id"
            FROM "Equipment" e
            LEFT JOIN "Borrowings" b ON b."EquipmentId" = e."Id"
            GROUP BY e."Id", e."IsAvailable"
            HAVING e."IsAvailable" = (COUNT(b."Id") FILTER (WHERE b."Status" = 'Active') > 0)
        ) bad
        """);

    /// <summary>
    /// Deterministic text dump of all three tables from a fresh connection. Take one before and
    /// one after a request: "no partial changes" is then simply Assert.Equal(before, after).
    /// </summary>
    public static async Task<string> SnapshotAsync()
    {
        await using var connection = await OpenFreshConnectionAsync();
        var text = new StringBuilder();

        await DumpAsync(connection, text, "Students",
            "SELECT * FROM \"Students\" ORDER BY \"Id\"");
        await DumpAsync(connection, text, "Equipment",
            "SELECT * FROM \"Equipment\" ORDER BY \"Id\"");
        await DumpAsync(connection, text, "Borrowings",
            "SELECT * FROM \"Borrowings\" ORDER BY \"Id\"");

        return text.ToString();
    }

    private static async Task DumpAsync(NpgsqlConnection connection, StringBuilder text, string title, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();

        text.AppendLine($"-- {title}");
        while (await reader.ReadAsync())
        {
            var cells = new string[reader.FieldCount];
            for (var i = 0; i < reader.FieldCount; i++)
                cells[i] = reader.IsDBNull(i) ? "NULL" : Convert.ToString(reader.GetValue(i)) ?? "";
            text.AppendLine(string.Join(" | ", cells));
        }
    }
}
