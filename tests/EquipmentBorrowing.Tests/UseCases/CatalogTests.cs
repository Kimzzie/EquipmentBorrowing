using System.Net;
using System.Text.Json;
using EquipmentBorrowing.Api.Contracts;
using EquipmentBorrowing.Tests.Infrastructure;

namespace EquipmentBorrowing.Tests.UseCases;

/// <summary>TC6-U01 Catalog and TC6-U02 Available filtering (UC-01, UC-02, API-01).</summary>
[Collection("Api")]
public sealed class CatalogTests : ApiTestBase
{
    public CatalogTests(ApiFactory factory) : base(factory) { }

    // ---------------- TC6-U01 ----------------

    [Fact, Trait("TC6", "U01")]
    public async Task U01_Catalog_returns_exactly_the_seven_seeded_items_with_availability()
    {
        var before = await TestDatabase.SnapshotAsync();

        var response = await Client.GetAsync("/api/v1/equipment");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Contract check on the raw JSON: names and shape, not just our own DTO class.
        using (var doc = JsonDocument.Parse(body))
        {
            Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
            Assert.Equal(7, doc.RootElement.GetArrayLength());
            foreach (var name in new[] { "id", "equipmentCode", "name", "description", "isAvailable" })
                Assert.True(doc.RootElement[0].TryGetProperty(name, out _), $"JSON property '{name}' is missing.");
        }

        var items = JsonSerializer.Deserialize<List<EquipmentDto>>(body, Json)!;

        var actual = items
            .OrderBy(i => i.EquipmentCode)
            .Select(i => (i.EquipmentCode, i.Name, i.IsAvailable))
            .ToArray();

        var expected = new[]
        {
            ("EQ-001", "Camera", false),
            ("EQ-002", "Laptop", true),
            ("EQ-003", "Projector", true),
            ("EQ-004", "Tablet", false),
            ("EQ-005", "Microphone", false),
            ("EQ-006", "Tripod", true),
            ("EQ-007", "Speaker", true)
        };

        Assert.Equal(expected, actual);
        Assert.Equal(7, items.Select(i => i.Id).Distinct().Count());   // identities are unique
        Assert.All(items, i => Assert.True(i.Id > 0));
        Assert.All(items, i => Assert.Null(i.Description));            // seeded without descriptions

        Assert.Equal(before, await TestDatabase.SnapshotAsync());      // read-only: nothing changed
    }

    [Fact, Trait("TC6", "U01")]
    public async Task U01_Empty_catalog_returns_200_and_an_empty_array()
    {
        await TestDatabase.ClearAllAsync();

        var response = await Client.GetAsync("/api/v1/equipment");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);   // empty is NOT an error
        Assert.Equal("[]", body.Trim());
        Assert.Equal(0, await TestDatabase.CountAsync("Equipment"));
    }

    // ---------------- TC6-U02 ----------------

    [Fact, Trait("TC6", "U02")]
    public async Task U02_Available_filter_includes_only_borrowable_items()
    {
        var before = await TestDatabase.SnapshotAsync();

        var response = await Client.GetAsync("/api/v1/equipment?available=true");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = await ReadAsync<List<EquipmentDto>>(response);
        var codes = items.Select(i => i.EquipmentCode).OrderBy(c => c).ToArray();

        Assert.Equal(new[] { "EQ-002", "EQ-003", "EQ-006", "EQ-007" }, codes);

        // Explicit exclusion of the three borrowed items (E1, E4, E5).
        Assert.DoesNotContain("EQ-001", codes);
        Assert.DoesNotContain("EQ-004", codes);
        Assert.DoesNotContain("EQ-005", codes);
        Assert.All(items, i => Assert.True(i.IsAvailable));

        // Cross-check against PostgreSQL itself.
        Assert.Equal(4, await TestDatabase.ScalarAsync<long>(
            "SELECT COUNT(*) FROM \"Equipment\" WHERE \"IsAvailable\""));

        Assert.Equal(before, await TestDatabase.SnapshotAsync());
    }

    [Fact, Trait("TC6", "U02")]
    public async Task U02_Available_filter_returns_an_empty_array_when_nothing_is_available()
    {
        // Setup only: mark everything unavailable, as if every item were on loan.
        await TestDatabase.ExecuteAsync("UPDATE \"Equipment\" SET \"IsAvailable\" = false");

        var response = await Client.GetAsync("/api/v1/equipment?available=true");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("[]", body.Trim());
    }
}
