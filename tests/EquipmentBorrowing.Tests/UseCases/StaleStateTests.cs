using System.Net;
using EquipmentBorrowing.Api.Contracts;
using EquipmentBorrowing.Tests.Infrastructure;
using Npgsql;

namespace EquipmentBorrowing.Tests.UseCases;

/// <summary>
/// TC6-E05 Stale and conflicting state (UC-03, UC-06; BR-10, 15, 16; API-10, API-11).
/// Three layers: (1) application revalidation rejects a request based on an out-of-date view,
/// (2) PostgreSQL's partial unique index rejects a second ACTIVE row for one item even when the API is
/// bypassed, and (3) genuinely simultaneous requests still produce exactly one winner.
/// </summary>
[Collection("Api")]
public sealed class StaleStateTests : ApiTestBase
{
    public StaleStateTests(ApiFactory factory) : base(factory) { }

    private Task<long> ActiveRowsForItemAsync(int equipmentId) => TestDatabase.ScalarAsync<long>(
        "SELECT COUNT(*) FROM \"Borrowings\" WHERE \"EquipmentId\" = @id AND \"Status\" = 'Active'", ("id", equipmentId));

    // ---------------- layer 1: application revalidation ----------------

    [Fact, Trait("TC6", "E05")]
    public async Task E05_A_stale_borrow_request_is_a_409_after_someone_else_took_the_item()
    {
        var s1 = await StudentIdAsync("ST-100");
        var s4 = await StudentIdAsync("ST-400");
        var e2 = await EquipmentIdAsync("EQ-002");

        // Staff member A looks at the catalog: E2 is available.
        var view = await ReadAsync<List<EquipmentDto>>(await Client.GetAsync("/api/v1/equipment?available=true"));
        Assert.Contains(view, e => e.EquipmentCode == "EQ-002");

        // Staff member B (another context) issues E2 first.
        var first = await BorrowAsync(s4, e2, D.AddDays(2));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var afterFirst = await TestDatabase.SnapshotAsync();

        // Staff member A submits from the now-stale view.
        var stale = await BorrowAsync(s1, e2, D.AddDays(2));

        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        var body = await stale.Content.ReadAsStringAsync();
        AssertSafeError(body);
        Assert.Equal("rule_violation", ErrorCode(body));

        // Fresh reads: still exactly one active record for E2, and it is B's. Nothing else changed.
        Assert.Equal(1, await ActiveRowsForItemAsync(e2));
        Assert.Equal(1, await TestDatabase.ActiveCountForStudentAsync(s4));
        Assert.Equal(1, await TestDatabase.ActiveCountForStudentAsync(s1));       // S1 still only has E1
        Assert.Equal(afterFirst, await TestDatabase.SnapshotAsync());
        Assert.Equal(0, await TestDatabase.ConsistencyViolationsAsync());
    }

    [Fact, Trait("TC6", "E05")]
    public async Task E05_A_stale_checkout_is_rejected_as_a_whole_and_the_other_item_stays_free()
    {
        var s1 = await StudentIdAsync("ST-100");
        var s4 = await StudentIdAsync("ST-400");
        var e2 = await EquipmentIdAsync("EQ-002");
        var e6 = await EquipmentIdAsync("EQ-006");

        Assert.Equal(HttpStatusCode.Created, (await BorrowAsync(s4, e2, D.AddDays(2))).StatusCode);   // E2 taken
        var before = await TestDatabase.SnapshotAsync();

        var stale = await CheckoutAsync(s1, new[] { e2, e6 }, D.AddDays(2));   // E2 taken, E6 still free

        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("rule_violation", ErrorCode(await stale.Content.ReadAsStringAsync()));

        // All-or-nothing: the FREE item E6 must not have been borrowed just because it was available.
        Assert.True(await TestDatabase.IsAvailableAsync(e6));
        Assert.Equal(0, await TestDatabase.ScalarAsync<long>(
            "SELECT COUNT(*) FROM \"Borrowings\" WHERE \"EquipmentId\" = @id", ("id", e6)));
        Assert.Equal(1, await ActiveRowsForItemAsync(e2));
        Assert.Equal(1, await TestDatabase.ActiveCountForStudentAsync(s1));
        Assert.Equal(before, await TestDatabase.SnapshotAsync());
    }

    // ---------------- layer 2: PostgreSQL itself ----------------

    [Fact, Trait("TC6", "E05")]
    public async Task E05_PostgreSQL_rejects_a_second_active_row_for_the_same_item_even_without_the_API()
    {
        var s1 = await StudentIdAsync("ST-100");
        var s4 = await StudentIdAsync("ST-400");
        var e2 = await EquipmentIdAsync("EQ-002");
        Assert.Equal(HttpStatusCode.Created, (await BorrowAsync(s4, e2, D.AddDays(2))).StatusCode);
        var before = await TestDatabase.SnapshotAsync();

        var error = await Assert.ThrowsAsync<PostgresException>(() => TestDatabase.ExecuteAsync($"""
            INSERT INTO "Borrowings" ("StudentId", "EquipmentId", "DateBorrowed", "ExpectedReturnDate", "Status")
            VALUES ({s1}, {e2}, '{Iso(D)}', '{Iso(D.AddDays(2))}', 'Active')
            """));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, error.SqlState);
        Assert.Equal("UX_Borrowings_OneActivePerEquipment", error.ConstraintName);
        Assert.Equal(1, await ActiveRowsForItemAsync(e2));
        Assert.Equal(before, await TestDatabase.SnapshotAsync());
    }

    [Fact, Trait("TC6", "E05")]
    public async Task E05_The_database_rule_is_about_ACTIVE_rows_so_an_item_keeps_unlimited_history()
    {
        // Returned rows are ignored by the partial index: that is what makes "one current holder, any history" work.
        var s1 = await StudentIdAsync("ST-100");
        var e3 = await EquipmentIdAsync("EQ-003");   // already has one returned row

        await TestDatabase.ExecuteAsync($"""
            INSERT INTO "Borrowings" ("StudentId", "EquipmentId", "DateBorrowed", "ExpectedReturnDate", "DateReturned", "Status")
            VALUES ({s1}, {e3}, '{Iso(D.AddDays(-20))}', '{Iso(D.AddDays(-15))}', '{Iso(D.AddDays(-16))}', 'Returned')
            """);

        Assert.Equal(2, await TestDatabase.ScalarAsync<long>(
            "SELECT COUNT(*) FROM \"Borrowings\" WHERE \"EquipmentId\" = @id", ("id", e3)));
        Assert.Equal(0, await ActiveRowsForItemAsync(e3));
    }

    // ---------------- layer 3: genuinely simultaneous requests ----------------

    [Fact, Trait("TC6", "E05")]
    public async Task E05_Two_simultaneous_borrow_requests_for_one_item_produce_exactly_one_winner()
    {
        // The interleaving is up to the scheduler, so repeat it. The invariants must hold EVERY time.
        for (var round = 1; round <= 6; round++)
        {
            await TestDatabase.ResetAsync();
            var s1 = await StudentIdAsync("ST-100");
            var s4 = await StudentIdAsync("ST-400");
            var e2 = await EquipmentIdAsync("EQ-002");

            var responses = await Task.WhenAll(
                BorrowAsync(s4, e2, D.AddDays(2)),
                BorrowAsync(s1, e2, D.AddDays(2)));

            var statuses = responses.Select(r => (int)r.StatusCode).OrderBy(s => s).ToArray();
            Assert.True(statuses.SequenceEqual(new[] { 201, 409 }),
                $"Round {round}: expected one 201 and one 409 but got [{string.Join(", ", statuses)}].");

            var loser = responses.Single(r => r.StatusCode == HttpStatusCode.Conflict);
            var code = ErrorCode(await loser.Content.ReadAsStringAsync());
            Assert.Contains(code, new[] { "rule_violation", "conflict" });

            Assert.Equal(1, await ActiveRowsForItemAsync(e2));                 // never two holders
            Assert.Equal(5, await TestDatabase.CountAsync("Borrowings"));      // 4 seeded + exactly 1 new
            Assert.Equal(0, await TestDatabase.ConsistencyViolationsAsync());
        }
    }

    [Fact, Trait("TC6", "E05")]
    public async Task E05_Two_simultaneous_checkouts_sharing_one_item_never_leave_a_partial_result()
    {
        for (var round = 1; round <= 6; round++)
        {
            await TestDatabase.ResetAsync();
            var s1 = await StudentIdAsync("ST-100");   // 1 active, may add 2
            var s4 = await StudentIdAsync("ST-400");
            var e2 = await EquipmentIdAsync("EQ-002");
            var e6 = await EquipmentIdAsync("EQ-006");
            var e7 = await EquipmentIdAsync("EQ-007");

            var responses = await Task.WhenAll(
                CheckoutAsync(s4, new[] { e2, e6 }, D.AddDays(2)),    // both want E2
                CheckoutAsync(s1, new[] { e2, e7 }, D.AddDays(2)));

            var statuses = responses.Select(r => (int)r.StatusCode).OrderBy(s => s).ToArray();
            Assert.True(statuses.SequenceEqual(new[] { 201, 409 }),
                $"Round {round}: expected one 201 and one 409 but got [{string.Join(", ", statuses)}].");

            // Exactly ONE winner owns E2 plus its own second item; the loser left no trace at all.
            Assert.Equal(1, await ActiveRowsForItemAsync(e2));
            Assert.Equal(6, await TestDatabase.CountAsync("Borrowings"));      // 4 seeded + the winner's 2
            var unavailableOfSecondItems =
                (await TestDatabase.IsAvailableAsync(e6) ? 0 : 1) + (await TestDatabase.IsAvailableAsync(e7) ? 0 : 1);
            Assert.Equal(1, unavailableOfSecondItems);                          // only the winner's second item
            Assert.Equal(0, await TestDatabase.ConsistencyViolationsAsync());
        }
    }
}
