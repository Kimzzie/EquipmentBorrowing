using System.Net;
using EquipmentBorrowing.Api.Contracts;
using EquipmentBorrowing.Tests.Infrastructure;

namespace EquipmentBorrowing.Tests.UseCases;

/// <summary>TC6-U15 Register equipment (API-07, API-08) and TC6-U16 Update equipment (API-07, 09, 16).</summary>
[Collection("Api")]
public sealed class EquipmentManagementTests : ApiTestBase
{
    public EquipmentManagementTests(ApiFactory factory) : base(factory) { }

    // ---------------- TC6-U15 ----------------

    [Fact, Trait("TC6", "U15")]
    public async Task U15_Register_equipment_returns_201_with_Location_is_available_and_is_durable()
    {
        var response = await RegisterEquipmentAsync("EQ-008", "Webcam", description: null);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await ReadAsync<EquipmentDto>(response);

        Assert.True(created.Id > 0);
        Assert.Equal("EQ-008", created.EquipmentCode);
        Assert.Equal("Webcam", created.Name);
        Assert.Null(created.Description);
        Assert.True(created.IsAvailable);             // BR-06: new items start available

        // Location header points at API-07 and returns the same item.
        var location = response.Headers.Location;
        Assert.NotNull(location);
        Assert.EndsWith($"/api/v1/equipment/{created.Id}", location!.ToString());

        var fetched = await Client.GetAsync(location);
        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
        Assert.Equal(created, await ReadAsync<EquipmentDto>(fetched));

        // Fresh PostgreSQL connection: one new available item with no borrowing.
        Assert.Equal(8, await TestDatabase.CountAsync("Equipment"));
        Assert.Equal(1, await TestDatabase.ScalarAsync<long>(
            "SELECT COUNT(*) FROM \"Equipment\" WHERE \"EquipmentCode\" = 'EQ-008'"));
        Assert.True(await TestDatabase.IsAvailableAsync(created.Id));
        Assert.Equal(1, await TestDatabase.ScalarAsync<long>(
            "SELECT COUNT(*) FROM \"Equipment\" WHERE \"Id\" = @id AND \"Description\" IS NULL", ("id", created.Id)));
        Assert.Equal(0, await TestDatabase.ScalarAsync<long>(
            "SELECT COUNT(*) FROM \"Borrowings\" WHERE \"EquipmentId\" = @id", ("id", created.Id)));
        Assert.Equal(0, await TestDatabase.ConsistencyViolationsAsync());

        // The new item shows up in the "available" view too (UC-02 cross-check).
        var available = await ReadAsync<List<EquipmentDto>>(await Client.GetAsync("/api/v1/equipment?available=true"));
        Assert.Contains(available, e => e.EquipmentCode == "EQ-008");
    }

    [Fact, Trait("TC6", "U15")]
    public async Task U15_Duplicate_identifier_is_rejected_with_409_and_adds_no_row()
    {
        var before = await TestDatabase.SnapshotAsync();

        var response = await RegisterEquipmentAsync("EQ-001", "Another Camera");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        AssertSafeError(body);
        Assert.Equal("duplicate", ErrorCode(body));

        Assert.Equal(before, await TestDatabase.SnapshotAsync());
        Assert.Equal(7, await TestDatabase.CountAsync("Equipment"));
    }

    // ---------------- TC6-U16 ----------------

    [Fact, Trait("TC6", "U16")]
    public async Task U16_Update_changes_descriptive_fields_but_keeps_availability_and_history()
    {
        var e1 = await EquipmentIdAsync("EQ-001");
        var historyBefore = await ReadAsync<List<BorrowingDto>>(
            await Client.GetAsync($"/api/v1/equipment/{e1}/borrowings"));
        var loan = Assert.Single(historyBefore);

        var update = await UpdateEquipmentAsync(e1, "EQ-001", "Camera Kit", "DSLR body with 50mm lens");

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await ReadAsync<EquipmentDto>(update);
        Assert.Equal(e1, updated.Id);                               // stable identity
        Assert.Equal("EQ-001", updated.EquipmentCode);
        Assert.Equal("Camera Kit", updated.Name);
        Assert.Equal("DSLR body with 50mm lens", updated.Description);
        Assert.False(updated.IsAvailable);                          // availability unchanged by an edit

        // History (API-16): the same single active record for S1.
        var historyAfter = await ReadAsync<List<BorrowingDto>>(
            await Client.GetAsync($"/api/v1/equipment/{e1}/borrowings"));
        var after = Assert.Single(historyAfter);
        Assert.Equal(loan.Id, after.Id);
        Assert.Equal("Active", after.Status);
        Assert.Equal("ST-100", after.StudentNumber);
        Assert.Equal(loan.BorrowedDate, after.BorrowedDate);
        Assert.Equal("Camera Kit", after.EquipmentName);            // joined name follows the edit

        // Fresh PostgreSQL connection: same row, still unavailable, loan intact, nothing added or lost.
        Assert.Equal("Camera Kit", await TestDatabase.ScalarAsync<string>(
            "SELECT \"Name\" FROM \"Equipment\" WHERE \"Id\" = @id", ("id", e1)));
        Assert.False(await TestDatabase.IsAvailableAsync(e1));
        Assert.Equal("Active", await TestDatabase.BorrowingStatusAsync(loan.Id));
        Assert.Equal(7, await TestDatabase.CountAsync("Equipment"));
        Assert.Equal(4, await TestDatabase.CountAsync("Borrowings"));
        Assert.Equal(0, await TestDatabase.ConsistencyViolationsAsync());
    }

    [Fact, Trait("TC6", "U16")]
    public async Task U16_Editing_an_available_item_does_not_borrow_or_return_it()
    {
        var e2 = await EquipmentIdAsync("EQ-002");

        var update = await UpdateEquipmentAsync(e2, "EQ-002", "Laptop Pro", "16-inch");

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.True((await ReadAsync<EquipmentDto>(update)).IsAvailable);
        Assert.True(await TestDatabase.IsAvailableAsync(e2));
        Assert.Equal(4, await TestDatabase.CountAsync("Borrowings"));   // an edit never creates a borrowing
    }
}
