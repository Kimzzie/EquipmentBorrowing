using System.Net.Sockets;
using Npgsql;

namespace EquipmentBorrowing.Infrastructure.Data;

/// <summary>
/// Turns provider exceptions into messages that are useful to staff and never
/// leak credentials (BR-18). Grows in later stages (duplicates, commit failures).
/// </summary>
internal static class DatabaseErrorTranslator
{
    public static string ToUserMessage(Exception exception)
    {
        // Provider errors are often wrapped, so look through the InnerException chain.
        for (var current = exception; current is not null; current = current.InnerException)
        {
            switch (current)
            {
                case PostgresException
                {
                    SqlState: PostgresErrorCodes.InvalidPassword
                        or PostgresErrorCodes.InvalidAuthorizationSpecification
                }:
                    return "PostgreSQL rejected the login. Check the username and password in your local connection settings.";

                case PostgresException { SqlState: PostgresErrorCodes.InvalidCatalogName }:
                    return "The PostgreSQL database does not exist yet. Create it first (see the README), then retry.";

                case PostgresException:
                    return "PostgreSQL reported an error while preparing the database. Check the server log for details.";

                case NpgsqlException or SocketException or TimeoutException:
                    return "Cannot reach the PostgreSQL server. Make sure it is running and that the host and port are correct, then retry.";
            }
        }

        return "An unexpected database error occurred.";
    }
}