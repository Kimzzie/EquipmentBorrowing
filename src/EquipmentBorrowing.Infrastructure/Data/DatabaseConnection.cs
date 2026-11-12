using System.Text.Json;

namespace EquipmentBorrowing.Infrastructure.Data;

/// <summary>
/// Finds the PostgreSQL connection string WITHOUT it ever being committed to Git.
/// Order: 1) environment variable, 2) appsettings.Local.json (git-ignored).
/// </summary>
public static class DatabaseConnection
{
    public const string EnvironmentVariableName = "EQUIPMENT_BORROWING_CONNECTION";
    public const string LocalSettingsFileName = "appsettings.Local.json";

    private static string? _cached;

    // "??=" only caches a successful result. If the file is missing, the exception
    // is thrown again on the next try, so fixing the file and pressing Retry works
    // without restarting the app.
    public static string GetConnectionString() => _cached ??= Resolve();

    private static string Resolve()
    {
        var fromEnvironment = Environment.GetEnvironmentVariable(EnvironmentVariableName);
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
            return fromEnvironment;

        var fromFile = ReadFromLocalFile();
        if (!string.IsNullOrWhiteSpace(fromFile))
            return fromFile;

        throw new DatabaseConfigurationException(
            $"No PostgreSQL connection is configured. Copy appsettings.Local.example.json to " +
            $"{LocalSettingsFileName} in the solution folder and fill in your values " +
            $"(or set the {EnvironmentVariableName} environment variable).");
    }

    private static string? ReadFromLocalFile()
    {
        var path = FindLocalSettingsFile();
        if (path is null)
            return null;

        try
        {
            using var stream = File.OpenRead(path);
            using var document = JsonDocument.Parse(stream);

            return document.RootElement
                .GetProperty("ConnectionStrings")
                .GetProperty("EquipmentBorrowing")
                .GetString();
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            // Deliberately do not include ex.Message or the file content in the text.
            throw new DatabaseConfigurationException(
                $"{LocalSettingsFileName} is not valid. It must contain " +
                "ConnectionStrings:EquipmentBorrowing (see appsettings.Local.example.json).");
        }
    }

    // Walks upward from the app's folder (bin/Debug/net10.0 -> ... -> solution folder),
    // then from the current directory (which is where `dotnet ef` runs). One file in the
    // solution folder therefore serves both the app and the migration tooling.
    private static string? FindLocalSettingsFile()
    {
        foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            for (var dir = new DirectoryInfo(start); dir is not null; dir = dir.Parent)
            {
                var candidate = Path.Combine(dir.FullName, LocalSettingsFileName);
                if (File.Exists(candidate))
                    return candidate;
            }
        }

        return null;
    }
}