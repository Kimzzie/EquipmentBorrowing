namespace EquipmentBorrowing.Infrastructure.Data;

public static class DatabasePathProvider
{
    public static string GetDatabasePath()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EquipmentBorrowing");

        Directory.CreateDirectory(folder);

        return Path.Combine(folder, "equipmentborrowing.db");
    }

    public static string GetConnectionString() => $"Data Source={GetDatabasePath()}";
}