using System.Net;
using EquipmentBorrowing.Tests.Infrastructure;

namespace EquipmentBorrowing.Tests.UseCases;

/// <summary>
/// TC6-E02 Limit boundaries (UC-03, UC-06; BR-08 to BR-12, BR-15). The limit is three ACTIVE items:
/// existing active records plus the distinct items requested must not exceed three (BR-09).
/// Fixture: S1 has 1 active (E1) and 1 returned (E3); S3 has 2 active (E4, E5).
/// </summary>
[Collection("Api")]
public sealed class LimitTests : ApiTestBase
{
    public LimitTests(ApiFactory factory) : base(factory) { }

    [Fact, Trait("TC6", "E02")]
    public async Task E02_Single_borrow_that_reaches_exactly_three_is_accepted()
    {
        var s3 = await StudentIdAsync("ST-300");
        var e2 = await EquipmentIdAsync("EQ-002");

        var response = await BorrowAsync(s3, e2, D.AddDays(2));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(3, await TestDatabase.ActiveCountForStudentAsync(s3));
        Assert.False(await TestDatabase.IsAvailableAsync(e2));
        Assert.Equal(0, await TestDatabase.ConsistencyViolationsAsync());
    }

    [Fact, Trait("TC6", "E02")]
    public async Task E02_Checkout_that_reaches_exactly_three_is_accepted_and_returned_records_do_not_count()
    {
        // S1 has 1 active + 1 returned: only the ACTIVE one counts, so 1 + 2 = 3 is allowed.
        var s1 = await StudentIdAsync("ST-100");
        var e2 = await EquipmentIdAsync("EQ-002");
        var e6 = await EquipmentIdAsync("EQ-006");

        var response = await CheckoutAsync(s1, new[] { e2, e6 }, D.AddDays(2));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(3, await TestDatabase.ActiveCountForStudentAsync(s1));
        Assert.False(await TestDatabase.IsAvailableAsync(e2));
        Assert.False(await TestDatabase.IsAvailableAsync(e6));
        Assert.Equal(4, await TestDatabase.BorrowingCountForStudentAsync(s1));   // 1 returned + 3 active
        Assert.Equal(0, await TestDatabase.ConsistencyViolationsAsync());
    }

    [Fact, Trait("TC6", "E02")]
    public async Task E02_Checkout_over_the_limit_is_a_409_and_leaves_no_partial_writes()
    {
        var s3 = await StudentIdAsync("ST-300");
        var e2 = await EquipmentIdAsync("EQ-002");
        var e6 = await EquipmentIdAsync("EQ-006");
        var before = await TestDatabase.SnapshotAsync();

        var response = await CheckoutAsync(s3, new[] { e2, e6 }, D.AddDays(2));   // 2 active + 2 = 4

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        AssertSafeError(body);
        Assert.Equal("rule_violation", ErrorCode(body));

        // Fresh reads: neither borrowing exists and neither item changed.
        Assert.Equal(2, await TestDatabase.ActiveCountForStudentAsync(s3));
        Assert.True(await TestDatabase.IsAvailableAsync(e2));
        Assert.True(await TestDatabase.IsAvailableAsync(e6));
        Assert.Equal(4, await TestDatabase.CountAsync("Borrowings"));
        Assert.Equal(before, await TestDatabase.SnapshotAsync());
    }

    [Fact, Trait("TC6", "E02")]
    public async Task E02_Checkout_that_is_one_over_the_limit_is_rejected()
    {
        var s1 = await StudentIdAsync("ST-100");   // 1 active
        var e2 = await EquipmentIdAsync("EQ-002");
        var e6 = await EquipmentIdAsync("EQ-006");
        var e7 = await EquipmentIdAsync("EQ-007");
        var before = await TestDatabase.SnapshotAsync();

        var response = await CheckoutAsync(s1, new[] { e2, e6, e7 }, D.AddDays(2));   // 1 + 3 = 4

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(before, await TestDatabase.SnapshotAsync());
    }

    [Fact, Trait("TC6", "E02")]
    public async Task E02_A_fourth_single_borrow_is_rejected()
    {
        var s3 = await StudentIdAsync("ST-300");
        var e2 = await EquipmentIdAsync("EQ-002");
        var e6 = await EquipmentIdAsync("EQ-006");

        Assert.Equal(HttpStatusCode.Created, (await BorrowAsync(s3, e2, D.AddDays(2))).StatusCode);   // reaches 3
        var atLimit = await TestDatabase.SnapshotAsync();

        var response = await BorrowAsync(s3, e6, D.AddDays(2));                                       // would be 4

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("rule_violation", ErrorCode(await response.Content.ReadAsStringAsync()));
        Assert.Equal(atLimit, await TestDatabase.SnapshotAsync());
        Assert.Equal(3, await TestDatabase.ActiveCountForStudentAsync(s3));
    }

    [Fact, Trait("TC6", "E02")]
    public async Task E02_Returning_an_item_frees_a_slot_under_the_limit()
    {
        var s3 = await StudentIdAsync("ST-300");
        var e2 = await EquipmentIdAsync("EQ-002");
        var e6 = await EquipmentIdAsync("EQ-006");

        await BorrowAsync(s3, e2, D.AddDays(2));                              // now at 3
        var e4Loan = await ActiveBorrowingIdAsync("EQ-004");
        Assert.Equal(HttpStatusCode.OK, (await ReturnAsync(e4Loan)).StatusCode);   // back to 2 active

        var response = await BorrowAsync(s3, e6, D.AddDays(2));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(3, await TestDatabase.ActiveCountForStudentAsync(s3));
        Assert.Equal(0, await TestDatabase.ConsistencyViolationsAsync());
    }
}
