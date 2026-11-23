using System.Net;
using EquipmentBorrowing.Api.Contracts;
using EquipmentBorrowing.Tests.Infrastructure;

namespace EquipmentBorrowing.Tests.UseCases;

/// <summary>
/// TC6-U04 Active borrowings and TC6-U09 Search and ordering (UC-04, UC-09, API-14).
///
/// Fixture borrowings in creation order (ids ascend with it):
///   b1 S1 / EQ-001  borrowed D-9  expected D-1  Active
///   b2 S1 / EQ-003  borrowed D-8  expected D+2  Returned
///   b3 S3 / EQ-004  borrowed D-7  expected D+2  Active   (ties b4 on BOTH dates)
///   b4 S3 / EQ-005  borrowed D-7  expected D+2  Active
///
/// DOCUMENTED AMBIGUITY: the contract says "stable borrowing ID breaks date ties" but does not say
/// whether the tie-break follows the sort direction. The implementation orders ties by Id in the
/// SAME direction as the date (desc date => desc Id). These tests assert that implemented rule.
/// </summary>
[Collection("Api")]
public sealed class BorrowingSearchTests : ApiTestBase
{
    private static readonly string[] AllDefaultOrder = { "EQ-001", "EQ-003", "EQ-004", "EQ-005" };

    public BorrowingSearchTests(ApiFactory factory) : base(factory) { }

    private async Task<List<BorrowingDto>> QueryAsync(string queryString = "")
    {
        var response = await Client.GetAsync("/api/v1/borrowings" + queryString);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadAsync<List<BorrowingDto>>(response);
    }

    private static string[] Codes(IEnumerable<BorrowingDto> items) =>
        items.Select(b => b.EquipmentCode).ToArray();

    // ---------------- TC6-U04 ----------------

    [Fact, Trait("TC6", "U04")]
    public async Task U04_Active_borrowings_are_E1_E4_E5_with_their_students_and_exclude_returned_E3()
    {
        var before = await TestDatabase.SnapshotAsync();

        var active = await QueryAsync("?status=Active");

        Assert.Equal(new[] { "EQ-001", "EQ-004", "EQ-005" }, Codes(active));   // default order
        Assert.DoesNotContain("EQ-003", Codes(active));                        // returned E3 excluded
        Assert.All(active, b => Assert.Equal("Active", b.Status));
        Assert.All(active, b => Assert.Null(b.ReturnedDate));

        // Related student and equipment details are joined correctly.
        Assert.Equal(new[] { "ST-100", "ST-300", "ST-300" }, active.Select(b => b.StudentNumber).ToArray());
        Assert.Equal(new[] { "Ada Cruz", "Cara Lim", "Cara Lim" }, active.Select(b => b.StudentName).ToArray());
        Assert.Equal(new[] { "Camera", "Tablet", "Microphone" }, active.Select(b => b.EquipmentName).ToArray());

        var e1 = active[0];
        Assert.Equal(D.AddDays(-9), e1.BorrowedDate);
        Assert.Equal(D.AddDays(-1), e1.ExpectedReturnDate);

        // Cross-check with PostgreSQL.
        Assert.Equal(3, await TestDatabase.ScalarAsync<long>(
            "SELECT COUNT(*) FROM \"Borrowings\" WHERE \"Status\" = 'Active'"));

        Assert.Equal(before, await TestDatabase.SnapshotAsync());
    }

    // ---------------- TC6-U09: search ----------------

    [Theory, Trait("TC6", "U09")]
    [InlineData("aDa", new[] { "EQ-001", "EQ-003" })]          // student name, partial, mixed case
    [InlineData("CRUZ", new[] { "EQ-001", "EQ-003" })]         // student name, partial
    [InlineData("cara", new[] { "EQ-004", "EQ-005" })]         // student name
    [InlineData("st-300", new[] { "EQ-004", "EQ-005" })]       // student number, mixed case
    [InlineData("-100", new[] { "EQ-001", "EQ-003" })]         // student number, partial
    [InlineData("CAME", new[] { "EQ-001" })]                   // equipment name, partial, upper case
    [InlineData("tAbL", new[] { "EQ-004" })]                   // equipment name, partial, mixed case
    [InlineData("  camera  ", new[] { "EQ-001" })]             // surrounding whitespace is trimmed
    public async Task U09_Search_matches_name_number_or_equipment_case_insensitively(
        string search, string[] expectedCodes)
    {
        var result = await QueryAsync($"?search={Uri.EscapeDataString(search)}");

        Assert.Equal(expectedCodes, Codes(result));
    }

    [Fact, Trait("TC6", "U09")]
    public async Task U09_No_matches_returns_200_and_an_empty_array()
    {
        var response = await Client.GetAsync("/api/v1/borrowings?search=zzz-no-such-text");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);   // empty state, not an error
        Assert.Equal("[]", (await response.Content.ReadAsStringAsync()).Trim());
    }

    // ---------------- TC6-U09: status filter and reset ----------------

    [Theory, Trait("TC6", "U09")]
    [InlineData("Active", new[] { "EQ-001", "EQ-004", "EQ-005" })]
    [InlineData("Returned", new[] { "EQ-003" })]
    [InlineData("All", new[] { "EQ-001", "EQ-003", "EQ-004", "EQ-005" })]
    public async Task U09_Status_filter_returns_the_right_membership(string status, string[] expectedCodes)
    {
        var result = await QueryAsync($"?status={status}");

        Assert.Equal(expectedCodes, Codes(result));
    }

    [Fact, Trait("TC6", "U09")]
    public async Task U09_Default_and_cleared_criteria_return_everything_in_default_order()
    {
        var defaults = await QueryAsync();

        Assert.Equal(AllDefaultOrder, Codes(defaults));   // default: All, borrowedDate, asc

        // "Clear": narrow the list, then request with no criteria again: the full list is back.
        var narrowed = await QueryAsync("?search=ada&status=Active");
        Assert.Single(narrowed);

        var cleared = await QueryAsync();
        Assert.Equal(Codes(defaults), Codes(cleared));
    }

    // ---------------- TC6-U09: sorting (includes equal-date ties) ----------------

    [Theory, Trait("TC6", "U09")]
    // borrowedDate: D-9, D-8, D-7, D-7  -> b3/b4 tie on borrowed date.
    [InlineData("borrowedDate", "asc", new[] { "EQ-001", "EQ-003", "EQ-004", "EQ-005" })]
    [InlineData("borrowedDate", "desc", new[] { "EQ-005", "EQ-004", "EQ-003", "EQ-001" })]
    // expectedReturnDate: D-1, D+2, D+2, D+2 -> b2/b3/b4 tie on expected date.
    [InlineData("expectedReturnDate", "asc", new[] { "EQ-001", "EQ-003", "EQ-004", "EQ-005" })]
    [InlineData("expectedReturnDate", "desc", new[] { "EQ-005", "EQ-004", "EQ-003", "EQ-001" })]
    public async Task U09_Sort_orders_by_date_with_a_stable_id_tie_break(
        string sort, string direction, string[] expectedOrder)
    {
        var result = await QueryAsync($"?sort={sort}&direction={direction}");

        Assert.Equal(expectedOrder, Codes(result));

        // Determinism: asking again gives the identical order.
        Assert.Equal(expectedOrder, Codes(await QueryAsync($"?sort={sort}&direction={direction}")));
    }

    [Fact, Trait("TC6", "U09")]
    public async Task U09_The_two_sort_fields_are_really_different_columns()
    {
        // With the plain fixture both date orderings happen to give the same sequence, so a
        // wrong-column bug could hide. Give EQ-004 and EQ-005 distinct expected dates (setup only).
        await TestDatabase.ExecuteAsync($"""
            UPDATE "Borrowings" SET "ExpectedReturnDate" = '{Iso(D.AddDays(1))}'
            WHERE "EquipmentId" = (SELECT "Id" FROM "Equipment" WHERE "EquipmentCode" = 'EQ-004')
            """);
        await TestDatabase.ExecuteAsync($"""
            UPDATE "Borrowings" SET "ExpectedReturnDate" = '{Iso(D.AddDays(5))}'
            WHERE "EquipmentId" = (SELECT "Id" FROM "Equipment" WHERE "EquipmentCode" = 'EQ-005')
            """);

        // Expected dates now: EQ-001 D-1, EQ-004 D+1, EQ-003 D+2, EQ-005 D+5.
        Assert.Equal(new[] { "EQ-001", "EQ-004", "EQ-003", "EQ-005" },
            Codes(await QueryAsync("?sort=expectedReturnDate&direction=asc")));
        Assert.Equal(new[] { "EQ-005", "EQ-003", "EQ-004", "EQ-001" },
            Codes(await QueryAsync("?sort=expectedReturnDate&direction=desc")));

        // Borrowed-date order is unaffected by the change.
        Assert.Equal(new[] { "EQ-001", "EQ-003", "EQ-004", "EQ-005" },
            Codes(await QueryAsync("?sort=borrowedDate&direction=asc")));
    }

    // ---------------- TC6-U09: combined criteria ----------------

    [Theory, Trait("TC6", "U09")]
    [InlineData("?search=ada&status=Active", new[] { "EQ-001" })]
    [InlineData("?search=cara&status=Returned", new string[0])]
    [InlineData("?search=st-300&status=Active&sort=borrowedDate&direction=desc", new[] { "EQ-005", "EQ-004" })]
    [InlineData("?search=ada&sort=borrowedDate&direction=desc", new[] { "EQ-003", "EQ-001" })]
    [InlineData("?search=ADA&status=returned&sort=expectedReturnDate&direction=asc", new[] { "EQ-003" })]
    public async Task U09_Criteria_combine(string queryString, string[] expectedCodes)
    {
        var result = await QueryAsync(queryString);

        Assert.Equal(expectedCodes, Codes(result));
    }

    // ---------------- TC6-U09: read-only ----------------

    [Fact, Trait("TC6", "U09")]
    public async Task U09_Searching_never_changes_the_database()
    {
        var before = await TestDatabase.SnapshotAsync();

        await QueryAsync("?search=ada");
        await QueryAsync("?status=Returned&sort=expectedReturnDate&direction=desc");
        await QueryAsync("?search=zzz");

        Assert.Equal(before, await TestDatabase.SnapshotAsync());
    }
}
