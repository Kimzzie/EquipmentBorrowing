using System.Net;
using System.Text.Json;
using EquipmentBorrowing.Api.Contracts;
using EquipmentBorrowing.Tests.Infrastructure;

namespace EquipmentBorrowing.Tests.UseCases;

/// <summary>TC6-U06 Atomic multi-item success (API-11). Rejections and rollback are E02-E05.</summary>
[Collection("Api")]
public sealed class CheckoutTests : ApiTestBase
{
    public CheckoutTests(ApiFactory factory) : base(factory) { }

    [Fact, Trait("TC6", "U06")]
    public async Task U06_Checkout_of_two_items_creates_two_active_records_and_marks_both_unavailable()
    {
        var s4 = await StudentIdAsync("ST-400");
        var e2 = await EquipmentIdAsync("EQ-002");
        var e6 = await EquipmentIdAsync("EQ-006");

        var response = await CheckoutAsync(s4, new[] { e2, e6 }, D.AddDays(2));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        // Contract shape: { "borrowings": [ ... ] } with one entry per item.
        using (var doc = JsonDocument.Parse(body))
            Assert.Equal(2, doc.RootElement.GetProperty("borrowings").GetArrayLength());

        var result = JsonSerializer.Deserialize<CheckoutResponse>(body, Json)!;
        var borrowings = result.Borrowings;

        Assert.Equal(2, borrowings.Count);
        Assert.Equal(2, borrowings.Select(b => b.Id).Distinct().Count());                 // two different records
        Assert.All(borrowings, b => Assert.True(b.Id > 0));
        Assert.All(borrowings, b => Assert.Equal(s4, b.StudentId));
        Assert.Equal(new[] { e2, e6 }.OrderBy(x => x), borrowings.Select(b => b.EquipmentId).OrderBy(x => x));
        Assert.All(borrowings, b => Assert.Equal("Active", b.Status));
        Assert.All(borrowings, b => Assert.Equal(D, b.BorrowedDate));
        Assert.All(borrowings, b => Assert.Equal(D.AddDays(2), b.ExpectedReturnDate));
        Assert.All(borrowings, b => Assert.Null(b.ReturnedDate));

        // Fresh PostgreSQL connection: both rows and both item states, committed together.
        foreach (var borrowing in borrowings)
            Assert.Equal("Active", await TestDatabase.BorrowingStatusAsync(borrowing.Id));

        Assert.Equal(2, await TestDatabase.ActiveCountForStudentAsync(s4));
        Assert.Equal(2, await TestDatabase.ScalarAsync<long>(
            "SELECT COUNT(DISTINCT \"EquipmentId\") FROM \"Borrowings\" WHERE \"StudentId\" = @id AND \"Status\" = 'Active'",
            ("id", s4)));                                                                  // one record per item
        Assert.False(await TestDatabase.IsAvailableAsync(e2));
        Assert.False(await TestDatabase.IsAvailableAsync(e6));
        Assert.Equal(6, await TestDatabase.CountAsync("Borrowings"));                      // 4 seeded + 2 new
        Assert.Equal(0, await TestDatabase.ConsistencyViolationsAsync());
    }

    [Fact, Trait("TC6", "U06")]
    public async Task U06_Checkout_leaves_unrelated_records_untouched()
    {
        var s4 = await StudentIdAsync("ST-400");
        var e2 = await EquipmentIdAsync("EQ-002");
        var e6 = await EquipmentIdAsync("EQ-006");
        var s3 = await StudentIdAsync("ST-300");
        var s3Before = await TestDatabase.BorrowingCountForStudentAsync(s3);
        var e7 = await EquipmentIdAsync("EQ-007");

        await CheckoutAsync(s4, new[] { e2, e6 }, D.AddDays(2));

        Assert.Equal(s3Before, await TestDatabase.BorrowingCountForStudentAsync(s3));
        Assert.True(await TestDatabase.IsAvailableAsync(e7));   // an item nobody asked for stays available
    }
}
