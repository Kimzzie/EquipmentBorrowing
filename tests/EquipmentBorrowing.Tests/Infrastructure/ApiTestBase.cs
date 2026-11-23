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

    /// <summary>The machine-readable "code" of an error body, e.g. "rule_violation".</summary>
    protected static string ErrorCode(string body)
    {
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("code").GetString()!;
    }

    // ---- request helpers (API-10, API-11, API-12, API-06) ----

    protected Task<HttpResponseMessage> BorrowAsync(int studentId, int equipmentId, DateOnly expected) =>
        Client.PostAsJsonAsync("/api/v1/borrowings",
            new { studentId, equipmentId, expectedReturnDate = Iso(expected) }, Json);

    protected Task<HttpResponseMessage> CheckoutAsync(int studentId, IEnumerable<int> equipmentIds, DateOnly expected) =>
        Client.PostAsJsonAsync("/api/v1/checkout-requests",
            new { studentId, equipmentIds = equipmentIds.ToArray(), expectedReturnDate = Iso(expected) }, Json);

    protected Task<HttpResponseMessage> ReturnAsync(int borrowingId) =>
        Client.PostAsJsonAsync($"/api/v1/borrowings/{borrowingId}/return", new { }, Json);

    protected Task<HttpResponseMessage> SetEligibilityAsync(int studentId, bool isEligible)
    {
        var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/students/{studentId}/eligibility")
        {
            Content = JsonContent.Create(new { isEligible }, options: Json)
        };
        return Client.SendAsync(request);
    }

    // ---- student / equipment management helpers (API-04, 05, 08, 09) ----
    // Parameters are nullable on purpose so tests can send blank or null values.

    protected Task<HttpResponseMessage> RegisterStudentAsync(string? studentNumber, string? fullName, string? contact = null) =>
        Client.PostAsJsonAsync("/api/v1/students", new { studentNumber, fullName, contact }, Json);

    protected Task<HttpResponseMessage> UpdateStudentAsync(int id, string? studentNumber, string? fullName, string? contact) =>
        Client.PutAsJsonAsync($"/api/v1/students/{id}", new { studentNumber, fullName, contact }, Json);

    protected Task<HttpResponseMessage> RegisterEquipmentAsync(string? equipmentCode, string? name, string? description = null) =>
        Client.PostAsJsonAsync("/api/v1/equipment", new { equipmentCode, name, description }, Json);

    protected Task<HttpResponseMessage> UpdateEquipmentAsync(int id, string? equipmentCode, string? name, string? description) =>
        Client.PutAsJsonAsync($"/api/v1/equipment/{id}", new { equipmentCode, name, description }, Json);

    /// <summary>Resolves the id of the ACTIVE borrowing of an item through the API.</summary>
    protected async Task<int> ActiveBorrowingIdAsync(string equipmentCode)
    {
        var active = await Client.GetFromJsonAsync<List<BorrowingDto>>("/api/v1/borrowings?status=Active", Json);
        return active!.Single(b => b.EquipmentCode == equipmentCode).Id;
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
