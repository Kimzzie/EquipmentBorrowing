using EquipmentBorrowing.Application.Exceptions;
using Npgsql;

namespace EquipmentBorrowing.Infrastructure.Data;

/// <summary>
/// DEMONSTRATION SEAM for the Lab 4 atomicity evidence (A06, A18). It does nothing unless
/// the EQUIPMENT_BORROWING_FAULT environment variable is set, and it refuses to act on any
/// database whose name does not end in "_test", so it can never damage real data.
///   BeforeCommit : fails AFTER the writes were sent, BEFORE the commit -> rolled back.
///   AfterCommit  : the commit succeeds, then the "acknowledgment is lost" -> unknown outcome.
/// </summary>
internal static class FaultInjection
{
    public const string EnvironmentVariableName = "EQUIPMENT_BORROWING_FAULT";
    private const string BeforeCommit = "BeforeCommit";
    private const string AfterCommit = "AfterCommit";

    // Runs BEFORE the commit, so refusing here is always safe (nothing is committed yet).
    public static void GuardBeforeCommit()
    {
        var requested = Environment.GetEnvironmentVariable(EnvironmentVariableName);
        if (string.IsNullOrWhiteSpace(requested))
            return;

        if (!IsDisposableDatabase())
            throw new DatabaseAccessException(
                $"{EnvironmentVariableName} is set, but the connected database name does not end in '_test', " +
                "so the simulated failure was refused. Nothing was saved. Unset the variable or use the test database.");
    }

    public static void TriggerBeforeCommit()
    {
        if (!IsRequested(BeforeCommit))
            return;

        throw new DatabaseAccessException(
            "SIMULATED failure after the writes were sent but before the commit. " +
            "The transaction was not committed; nothing was saved.");
    }

    public static void TriggerAfterCommit()
    {
        if (!IsRequested(AfterCommit))
            return;

        throw new CommitOutcomeUnknownException(
            "SIMULATED: the connection was lost right after COMMIT was sent, so it is not known whether the " +
            "change was saved. Nothing was retried. Refresh and check the records before trying again.");
    }

    private static bool IsRequested(string point) =>
        string.Equals(
            Environment.GetEnvironmentVariable(EnvironmentVariableName), point, StringComparison.OrdinalIgnoreCase);

    private static bool IsDisposableDatabase()
    {
        var database = new NpgsqlConnectionStringBuilder(DatabaseConnection.GetConnectionString()).Database;
        return database is not null && database.EndsWith("_test", StringComparison.OrdinalIgnoreCase);
    }
}