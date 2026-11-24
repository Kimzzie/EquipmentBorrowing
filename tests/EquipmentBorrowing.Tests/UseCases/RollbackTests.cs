using System.Net;
using EquipmentBorrowing.Api.Contracts;
using EquipmentBorrowing.Tests.Infrastructure;

namespace EquipmentBorrowing.Tests.UseCases;

/// <summary>
/// TC6-E04 PostgreSQL rollback (UC-06; BR-12, 15, 16, 18; API-11).
///
/// The seam fires AFTER SaveChanges (the INSERTs and UPDATEs were really sent to PostgreSQL inside the
/// transaction) and BEFORE Commit. A 500 alone proves nothing about rollback, so every test here
/// reads the database again through a brand-new connection.
///
/// PROOF THAT A WRITE REALLY HAPPENED BEFORE THE FAULT: identity sequences are not transactional in
/// PostgreSQL. If the failed checkout had really INSERTed two rows, the sequence moved on even though the
/// rows were rolled back, so the NEXT successful checkout gets ids 7 and 8, not 5 and 6.
/// </summary>
[Collection("Api")]
public sealed class RollbackTests : ApiTestBase
{
    public RollbackTests(ApiFactory factory) : base(factory) { }

    [Fact, Trait("TC6", "E04")]
    public async Task E04_A_failure_after_the_writes_were_sent_rolls_back_the_whole_checkout()
    {
        var s4 = await StudentIdAsync("ST-400");
        var e2 = await EquipmentIdAsync("EQ-002");
        var e6 = await EquipmentIdAsync("EQ-006");
        var baseline = await TestDatabase.SnapshotAsync();

        HttpResponseMessage failed;
        using (FaultScope.BeforeCommit())
            failed = await CheckoutAsync(s4, new[] { e2, e6 }, D.AddDays(2));

        // 1. The reachable API answers with a SAFE 500 (a received response, not an unreachable server).
        var body = await failed.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
        AssertSafeError(body);
        Assert.Equal("database_error", ErrorCode(body));

        // 2. Independent fresh read: identical to the baseline. No rows, no availability change.
        Assert.Equal(baseline, await TestDatabase.SnapshotAsync());
        Assert.Equal(4, await TestDatabase.CountAsync("Borrowings"));
        Assert.Equal(0, await TestDatabase.ActiveCountForStudentAsync(s4));
        Assert.True(await TestDatabase.IsAvailableAsync(e2));
        Assert.True(await TestDatabase.IsAvailableAsync(e6));
        Assert.Equal(0, await TestDatabase.ConsistencyViolationsAsync());

        // 3. The fault really came AFTER the writes: the rolled-back INSERTs consumed ids 5 and 6.
        Assert.Null(Environment.GetEnvironmentVariable(FaultScope.VariableName));   // seam is disarmed again
        var retry = await CheckoutAsync(s4, new[] { e2, e6 }, D.AddDays(2));
        Assert.Equal(HttpStatusCode.Created, retry.StatusCode);                      // and the system recovered
        var created = (await ReadAsync<CheckoutResponse>(retry)).Borrowings;
        Assert.All(created, b => Assert.True(b.Id >= 7,
            $"Borrowing id {b.Id}: ids 5 and 6 should have been consumed by the rolled-back INSERTs."));
        Assert.Equal(0, await TestDatabase.ConsistencyViolationsAsync());
    }

    [Fact, Trait("TC6", "E04")]
    public async Task E04_A_failure_leaves_every_other_record_exactly_as_it_was()
    {
        var s4 = await StudentIdAsync("ST-400");
        var e2 = await EquipmentIdAsync("EQ-002");
        var e6 = await EquipmentIdAsync("EQ-006");

        using (FaultScope.BeforeCommit())
            await CheckoutAsync(s4, new[] { e2, e6 }, D.AddDays(2));

        // Seeded history is intact: S1 still has E1 active and E3 returned; S3 still has E4/E5 active.
        Assert.Equal(1, await TestDatabase.ActiveCountForStudentAsync(await StudentIdAsync("ST-100")));
        Assert.Equal(2, await TestDatabase.ActiveCountForStudentAsync(await StudentIdAsync("ST-300")));
        Assert.False(await TestDatabase.IsAvailableAsync(await EquipmentIdAsync("EQ-001")));
        Assert.True(await TestDatabase.IsAvailableAsync(await EquipmentIdAsync("EQ-007")));
    }

    [Fact, Trait("TC6", "E04")]
    public async Task E04_A_failure_during_a_single_borrow_saves_nothing()
    {
        var s4 = await StudentIdAsync("ST-400");
        var e2 = await EquipmentIdAsync("EQ-002");
        var baseline = await TestDatabase.SnapshotAsync();

        HttpResponseMessage failed;
        using (FaultScope.BeforeCommit())
            failed = await BorrowAsync(s4, e2, D.AddDays(2));

        Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
        Assert.Equal("database_error", ErrorCode(await failed.Content.ReadAsStringAsync()));
        Assert.Equal(baseline, await TestDatabase.SnapshotAsync());
        Assert.True(await TestDatabase.IsAvailableAsync(e2));
    }

    [Fact, Trait("TC6", "E04")]
    public async Task E04_A_failure_during_a_return_keeps_the_borrowing_active_and_the_item_unavailable()
    {
        var e1 = await EquipmentIdAsync("EQ-001");
        var loan = await ActiveBorrowingIdAsync("EQ-001");
        var baseline = await TestDatabase.SnapshotAsync();

        HttpResponseMessage failed;
        using (FaultScope.BeforeCommit())
            failed = await ReturnAsync(loan);

        Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
        Assert.Equal("database_error", ErrorCode(await failed.Content.ReadAsStringAsync()));
        Assert.Equal(baseline, await TestDatabase.SnapshotAsync());
        Assert.Equal("Active", await TestDatabase.BorrowingStatusAsync(loan));
        Assert.False(await TestDatabase.IsAvailableAsync(e1));

        // Recovery: the same return works once the fault is gone.
        Assert.Equal(HttpStatusCode.OK, (await ReturnAsync(loan)).StatusCode);
        Assert.Equal("Returned", await TestDatabase.BorrowingStatusAsync(loan));
    }
}
