using Microsoft.AspNetCore.Mvc.Testing;

namespace EquipmentBorrowing.Tests.Infrastructure;

/// <summary>
/// Hosts your REAL API (real routing, controllers, exception handler, EF Core, PostgreSQL)
/// inside the test process. Nothing is mocked.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public ApiFactory()
    {
        // Safety guard first: throws if the connection variable is missing or not a *_test database.
        _ = TestDatabase.ConnectionString;

        // Program.cs reads this at startup, so it must be set BEFORE the host is built.
        // A process environment variable is used (instead of UseSetting) because Program reads
        // builder.Configuration before ConfigureWebHost callbacks run.
        Environment.SetEnvironmentVariable(
            "EQUIPMENT_BORROWING_FIXED_DATE", TestDatabase.D.ToString("yyyy-MM-dd"));
    }
}
