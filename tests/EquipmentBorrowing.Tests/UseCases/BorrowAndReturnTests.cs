using System.Net;
using System.Net.Http.Json;
using EquipmentBorrowing.Api.Contracts;
using EquipmentBorrowing.Tests.Infrastructure;

namespace EquipmentBorrowing.Tests.UseCases;

/// <summary>TC6-U03 Single borrowing (API-10, API-13) and TC6-U05 Return and history (API-12, API-13).</summary>
[Collection("Api")]
public sealed class BorrowAndReturnTests : ApiTestBase
{
    public BorrowAndReturnTests(ApiFactory factory) : base(factory) { }

    // ---------------- TC6-U03 ----------------

    [Fact, Trait("TC6", "U03")]
    public async Task U03_Single_borrow_returns_201_with_Location_and_persists_one_active_record()
    {
        var s4 = await StudentIdAsync("ST-400");
        var e2 = await EquipmentIdAsync("EQ-002");

        var response = await BorrowAsync(s4, e2, D.AddDays(2));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await ReadAsync<BorrowingDto>(response);

        // The response: a generated id, the references, SERVER-decided checkout date (= D), Active.
        Assert.True(created.Id > 0);
        Assert.Equal(s4, created.StudentId);
        Assert.Equal(e2, created.EquipmentId);
        Assert.Equal("ST-400", created.StudentNumber);
        Assert.Equal("EQ-002", created.EquipmentCode);
        Assert.Equal(D, created.BorrowedDate);
        Assert.Equal(D.AddDays(2), created.ExpectedReturnDate);
        Assert.Null(created.ReturnedDate);
        Assert.Equal("Active", created.Status);

        // Location header points at API-13 and that URL returns the same borrowing.
        var location = response.Headers.Location;
        Assert.NotNull(location);
        Assert.EndsWith($"/api/v1/borrowings/{created.Id}", location!.ToString());

        var fetched = await Client.GetAsync(location);
        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
        Assert.Equal(created, await ReadAsync<BorrowingDto>(fetched));

        // Fresh PostgreSQL connection: the row and the item state were really committed.
        Assert.Equal("Active", await TestDatabase.BorrowingStatusAsync(created.Id));
        Assert.False(await TestDatabase.IsAvailableAsync(e2));
        Assert.Equal(1, await TestDatabase.ActiveCountForStudentAsync(s4));
        Assert.Equal(5, await TestDatabase.CountAsync("Borrowings"));   // 4 seeded + 1 new
        Assert.Equal(0, await TestDatabase.ConsistencyViolationsAsync());
    }

    // ---------------- TC6-U05 ----------------

    [Fact, Trait("TC6", "U05")]
    public async Task U05_Return_marks_the_same_record_returned_with_the_server_date_and_frees_the_item()
    {
        var s1 = await StudentIdAsync("ST-100");
        var e1 = await EquipmentIdAsync("EQ-001");
        var borrowingId = await ActiveBorrowingIdAsync("EQ-001");

        var response = await ReturnAsync(borrowingId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var returned = await ReadAsync<BorrowingDto>(response);

        // Same record (identity), original facts untouched, transition + date set by the server.
        Assert.Equal(borrowingId, returned.Id);
        Assert.Equal(s1, returned.StudentId);
        Assert.Equal(e1, returned.EquipmentId);
        Assert.Equal(D.AddDays(-9), returned.BorrowedDate);
        Assert.Equal(D.AddDays(-1), returned.ExpectedReturnDate);
        Assert.Equal("Returned", returned.Status);
        Assert.Equal(D, returned.ReturnedDate);

        // GET shows the same state (API-13).
        var fetched = await ReadAsync<BorrowingDto>(await Client.GetAsync($"/api/v1/borrowings/{borrowingId}"));
        Assert.Equal(returned, fetched);

        // Fresh PostgreSQL connection.
        Assert.Equal("Returned", await TestDatabase.BorrowingStatusAsync(borrowingId));
        Assert.Equal(Iso(D), await TestDatabase.ReturnedDateAsync(borrowingId));
        Assert.True(await TestDatabase.IsAvailableAsync(e1));
        Assert.Equal(4, await TestDatabase.CountAsync("Borrowings"));   // retained, nothing deleted
        Assert.Equal(2, await TestDatabase.BorrowingCountForStudentAsync(s1));
        Assert.Equal(0, await TestDatabase.ConsistencyViolationsAsync());
    }

    [Fact, Trait("TC6", "U05")]
    public async Task U05_Repeat_return_is_rejected_with_409_and_changes_nothing()
    {
        var borrowingId = await ActiveBorrowingIdAsync("EQ-001");

        Assert.Equal(HttpStatusCode.OK, (await ReturnAsync(borrowingId)).StatusCode);
        var afterFirstReturn = await TestDatabase.SnapshotAsync();

        var repeat = await ReturnAsync(borrowingId);

        Assert.Equal(HttpStatusCode.Conflict, repeat.StatusCode);
        var body = await repeat.Content.ReadAsStringAsync();
        AssertSafeError(body);
        Assert.Equal("rule_violation", ErrorCode(body));

        Assert.Equal(afterFirstReturn, await TestDatabase.SnapshotAsync());   // repeat caused no change
    }

    [Fact, Trait("TC6", "U05")]
    public async Task U05_Returned_borrowing_stays_in_history_and_out_of_the_active_list()
    {
        var s1 = await StudentIdAsync("ST-100");
        var borrowingId = await ActiveBorrowingIdAsync("EQ-001");

        await ReturnAsync(borrowingId);

        var active = await ReadAsync<List<BorrowingDto>>(await Client.GetAsync("/api/v1/borrowings?status=Active"));
        Assert.DoesNotContain(active, b => b.Id == borrowingId);

        var history = await ReadAsync<List<BorrowingDto>>(await Client.GetAsync($"/api/v1/students/{s1}/borrowings"));
        var record = history.Single(b => b.Id == borrowingId);
        Assert.Equal("Returned", record.Status);
        Assert.Equal(2, history.Count);
    }
}
