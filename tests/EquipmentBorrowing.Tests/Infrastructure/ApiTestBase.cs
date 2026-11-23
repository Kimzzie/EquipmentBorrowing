using System.Net.Http.Json;
using System.Text.Json;
using EquipmentBorrowing.Api.Contracts;

namespace EquipmentBorrowing.Tests.Infrastructure;

// ONE host for the whole run (cheap), but the DATABASE is reset before every single test.
[CollectionDefinition("Api")]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>;

/// <summary>
/// Base class for every HTTP + PostgreSQL test. xUnit creates a new instance per test,
/// so InitializeAsync() resets the dataset each time: no test depends on another.
/// </summary>
public abstract class ApiTestBase : IAsyncLifetime
{
    protected static readonly DateOnly D = TestDatabase.D;

    protected static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    protected ApiTestBase(ApiFactory factory)
    {
        Client = factory.CreateClient();
    }

    protected HttpClient Client { get; }

    public Task InitializeAsync() => TestDatabase.ResetAsync();

    public Task DisposeAsync()
    {
        Client.Dispose();
        return Task.CompletedTask;
    }

    protected static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd");

    /// <summary>The contract's error body is { "code": "...", "message": "..." } and leaks nothing.</summary>
    protected static void AssertSafeError(string body)
    {
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("code").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("message").GetString()));
        Assert.DoesNotContain("   at ", body);                                   // no stack trace
        Assert.DoesNotContain("Password", body, StringComparison.OrdinalIgnoreCase);
    }

    protected static async Task<T> ReadAsync<T>(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(Json))!;

    // The suite says "use IDs resolved from the API", never literal database keys.

    protected async Task<int> StudentIdAsync(string studentNumber)
    {
        var students = await Client.GetFromJsonAsync<List<StudentDto>>(
            $"/api/v1/students?search={Uri.EscapeDataString(studentNumber)}", Json);
        return students!.Single(s => s.StudentNumber == studentNumber).Id;
    }

    protected async Task<int> EquipmentIdAsync(string equipmentCode)
    {
        var equipment = await Client.GetFromJsonAsync<List<EquipmentDto>>("/api/v1/equipment", Json);
        return equipment!.Single(e => e.EquipmentCode == equipmentCode).Id;
    }
}
