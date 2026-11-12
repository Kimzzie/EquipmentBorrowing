using System.Data.Common;
using System.Net.Sockets;
using EquipmentBorrowing.Application.Exceptions;

namespace EquipmentBorrowing.Infrastructure.Data;

/// <summary>
/// Wraps READ queries so a connection problem reaches the ViewModel as a
/// DatabaseAccessException with a safe message, instead of a raw Npgsql exception.
/// That lets the UI show "database error + retry" and keep it distinct from "no results".
/// </summary>
internal static class DatabaseGuard
{
    public static async Task<T> ReadAsync<T>(Func<Task<T>> read)
    {
        try
        {
            return await read();
        }
        catch (Exception ex) when (ex is DbException or SocketException or IOException or TimeoutException)
        {
            throw new DatabaseAccessException(DatabaseErrorTranslator.ToUserMessage(ex), ex);
        }
    }
}