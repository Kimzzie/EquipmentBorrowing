using System.Net;
using System.Text.Json;
using EquipmentBorrowing.Api.Contracts;
using EquipmentBorrowing.Tests.Infrastructure;

namespace EquipmentBorrowing.Tests.UseCases;

/// <summary>TC6-U14 Eligibility transitions (API-06, API-10, API-12).</summary>
[Collection("Api")]
public sealed class EligibilityTests : ApiTestBase
{
    public EligibilityTests(ApiFactory factory) : base(factory) { }

    [Fact, Trait("TC6", "U14")]
    public async Task U14_Restrict_block_new_loan_allow_return_permit_and_issue_again()
    {
        var s1 = await StudentIdAsync("ST-100");
        var e1 = await EquipmentIdAsync("EQ-001");
        var e2 = await EquipmentIdAsync("EQ-002");
        var originalLoanId = await ActiveBorrowingIdAsync("EQ-001");

        // 1. Restrict S1 (200). The new state is persisted; the existing loan is NOT cancelled.
        var restrict = await SetEligibilityAsync(s1, false);
        Assert.Equal(HttpStatusCode.OK, restrict.StatusCode);
        Assert.False((await ReadAsync<StudentDto>(restrict)).IsEligible);
        Assert.False(await TestDatabase.IsAllowedToBorrowAsync(s1));
        Assert.Equal("Active", await TestDatabase.BorrowingStatusAsync(originalLoanId));
        Assert.False(await TestDatabase.IsAvailableAsync(e1));
        var afterRestrict = await TestDatabase.SnapshotAsync();

        // 2. A new issue is rejected (409) and nothing changes.
        var blocked = await BorrowAsync(s1, e2, D.AddDays(2));
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        var blockedBody = await blocked.Content.ReadAsStringAsync();
        AssertSafeError(blockedBody);
        Assert.Equal("rule_violation", ErrorCode(blockedBody));
        Assert.Equal(afterRestrict, await TestDatabase.SnapshotAsync());

        // 3. A restricted student can still RETURN (200): same record, history kept, item free.
        var returned = await ReturnAsync(originalLoanId);
        Assert.Equal(HttpStatusCode.OK, returned.StatusCode);
        var returnedDto = await ReadAsync<BorrowingDto>(returned);
        Assert.Equal(originalLoanId, returnedDto.Id);
        Assert.Equal("Returned", returnedDto.Status);
        Assert.Equal("Returned", await TestDatabase.BorrowingStatusAsync(originalLoanId));
        Assert.True(await TestDatabase.IsAvailableAsync(e1));
        Assert.Equal(2, await TestDatabase.BorrowingCountForStudentAsync(s1));   // history retained

        // 4. Permit S1 again (200).
        var permit = await SetEligibilityAsync(s1, true);
        Assert.Equal(HttpStatusCode.OK, permit.StatusCode);
        Assert.True((await ReadAsync<StudentDto>(permit)).IsEligible);
        Assert.True(await TestDatabase.IsAllowedToBorrowAsync(s1));

        // 5. The same otherwise-valid issue now succeeds (201).
        var issue = await BorrowAsync(s1, e2, D.AddDays(2));
        Assert.Equal(HttpStatusCode.Created, issue.StatusCode);
        var issued = await ReadAsync<BorrowingDto>(issue);
        Assert.Equal("Active", await TestDatabase.BorrowingStatusAsync(issued.Id));
        Assert.False(await TestDatabase.IsAvailableAsync(e2));
        Assert.Equal(3, await TestDatabase.BorrowingCountForStudentAsync(s1));   // E3 returned, E1 returned, E2 active
        Assert.Equal(1, await TestDatabase.ActiveCountForStudentAsync(s1));
        Assert.Equal(0, await TestDatabase.ConsistencyViolationsAsync());
    }

    [Fact, Trait("TC6", "U14")]
    public async Task U14_Repeating_the_same_eligibility_is_a_harmless_no_op()
    {
        var s1 = await StudentIdAsync("ST-100");

        var permitAgain = await SetEligibilityAsync(s1, true);          // already eligible
        Assert.Equal(HttpStatusCode.OK, permitAgain.StatusCode);
        Assert.True((await ReadAsync<StudentDto>(permitAgain)).IsEligible);

        await SetEligibilityAsync(s1, false);
        var afterFirstRestrict = await TestDatabase.SnapshotAsync();

        var restrictAgain = await SetEligibilityAsync(s1, false);       // already restricted
        Assert.Equal(HttpStatusCode.OK, restrictAgain.StatusCode);
        Assert.False((await ReadAsync<StudentDto>(restrictAgain)).IsEligible);

        Assert.Equal(afterFirstRestrict, await TestDatabase.SnapshotAsync());
    }

    [Fact, Trait("TC6", "U14")]
    public async Task U14_Ineligible_student_S2_is_rejected_for_a_new_loan_from_the_start()
    {
        var s2 = await StudentIdAsync("ST-200");
        var e2 = await EquipmentIdAsync("EQ-002");
        var before = await TestDatabase.SnapshotAsync();

        var response = await BorrowAsync(s2, e2, D.AddDays(2));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(before, await TestDatabase.SnapshotAsync());
    }

    [Fact, Trait("TC6", "U14")]
    public async Task U14_Eligibility_of_a_missing_student_returns_404()
    {
        var response = await SetEligibilityAsync(999_999, true);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        AssertSafeError(await response.Content.ReadAsStringAsync());
    }
}
