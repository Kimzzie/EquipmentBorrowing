using System.Net;
using EquipmentBorrowing.Tests.Infrastructure;

namespace EquipmentBorrowing.Tests.UseCases;

/// <summary>
/// TC6-E03 Protocol errors (BR-08 to BR-12, BR-17, BR-18). Every rejected request must give the
/// documented status, the machine-readable code, a SAFE body, and leave PostgreSQL untouched.
/// D is 2026-11-10, so "2026-11-12" below is D+2.
/// </summary>
[Collection("Api")]
public sealed class ProtocolTests : ApiTestBase
{
    public ProtocolTests(ApiFactory factory) : base(factory) { }

    // ---- requests that need no resolved ids (the failure happens before any lookup) ----

    [Theory, Trait("TC6", "E03")]
    // 400: malformed JSON, unknown / protected fields, wrong types, blank required values
    [InlineData("POST", "/api/v1/students", "{ this is not json", 400, "validation_failed")]
    [InlineData("POST", "/api/v1/students", "{\"studentNumber\":\"ST-600\",\"fullName\":\"X\",\"id\":99}", 400, "validation_failed")]
    [InlineData("POST", "/api/v1/students", "{\"studentNumber\":\"ST-600\",\"fullName\":123}", 400, "validation_failed")]
    [InlineData("POST", "/api/v1/students", "{\"studentNumber\":\"\",\"fullName\":\"X\"}", 400, "validation_failed")]
    [InlineData("POST", "/api/v1/students", "{\"studentNumber\":\"ST-600\",\"fullName\":\"   \"}", 400, "validation_failed")]
    [InlineData("POST", "/api/v1/students", "{\"studentNumber\":\"ST-600\",\"fullName\":\"X\",\"isEligible\":false}", 400, "validation_failed")]
    [InlineData("POST", "/api/v1/equipment", "{\"equipmentCode\":\"EQ-600\",\"name\":\"\"}", 400, "validation_failed")]
    [InlineData("POST", "/api/v1/equipment", "{\"equipmentCode\":\"EQ-600\",\"name\":\"X\",\"isAvailable\":false}", 400, "validation_failed")]
    [InlineData("POST", "/api/v1/borrowings", "{}", 400, "validation_failed")]
    [InlineData("POST", "/api/v1/borrowings", "{\"studentId\":1,\"equipmentId\":2,\"expectedReturnDate\":\"not-a-date\"}", 400, "validation_failed")]
    [InlineData("POST", "/api/v1/borrowings/1/return", "{\"returnedDate\":\"2026-01-01\"}", 400, "validation_failed")]
    [InlineData("POST", "/api/v1/checkout-requests", "{\"studentId\":1,\"equipmentIds\":\"two\",\"expectedReturnDate\":\"2026-11-12\"}", 400, "validation_failed")]
    [InlineData("PATCH", "/api/v1/students/1/eligibility", "{}", 400, "validation_failed")]
    [InlineData("PATCH", "/api/v1/students/1/eligibility", "{\"isEligible\":\"yes\"}", 400, "validation_failed")]
    [InlineData("GET", "/api/v1/equipment?available=maybe", null, 400, "validation_failed")]
    [InlineData("GET", "/api/v1/borrowings?status=Bogus", null, 400, "validation_failed")]
    [InlineData("GET", "/api/v1/borrowings?sort=name", null, 400, "validation_failed")]
    [InlineData("GET", "/api/v1/borrowings?direction=sideways", null, 400, "validation_failed")]
    // 404: missing records
    [InlineData("GET", "/api/v1/borrowings/999999", null, 404, "not_found")]
    [InlineData("POST", "/api/v1/borrowings/999999/return", "{}", 404, "not_found")]
    [InlineData("PUT", "/api/v1/students/999999", "{\"studentNumber\":\"ST-900\",\"fullName\":\"X\"}", 404, "not_found")]
    [InlineData("PUT", "/api/v1/equipment/999999", "{\"equipmentCode\":\"EQ-900\",\"name\":\"X\"}", 404, "not_found")]
    [InlineData("POST", "/api/v1/borrowings", "{\"studentId\":999999,\"equipmentId\":999999,\"expectedReturnDate\":\"2026-11-12\"}", 404, "not_found")]
    [InlineData("POST", "/api/v1/checkout-requests", "{\"studentId\":999999,\"equipmentIds\":[999998,999999],\"expectedReturnDate\":\"2026-11-12\"}", 404, "not_found")]
    // 404: no such endpoint
    [InlineData("GET", "/api/v1/nothing-here", null, 404, "route_not_found")]
    [InlineData("GET", "/api/v2/equipment", null, 404, "route_not_found")]
    // 405: the route exists but not for this method
    [InlineData("DELETE", "/api/v1/students", null, 405, "method_not_allowed")]
    [InlineData("PATCH", "/api/v1/equipment", null, 405, "method_not_allowed")]
    [InlineData("PUT", "/api/v1/borrowings", null, 405, "method_not_allowed")]
    public async Task E03_Protocol_errors_give_the_documented_status_code_and_a_safe_body(
        string method, string url, string? body, int expectedStatus, string expectedCode)
    {
        var before = await TestDatabase.SnapshotAsync();

        var (response, text) = await SendRawAsync(new HttpMethod(method), url, body);

        Assert.Equal(expectedStatus, (int)response.StatusCode);
        AssertSafeError(text);
        Assert.Equal(expectedCode, ErrorCode(text));
        Assert.Equal(before, await TestDatabase.SnapshotAsync());      // a rejected request never writes
    }

    [Fact, Trait("TC6", "E03")]
    public async Task E03_A_body_that_is_not_JSON_is_a_415()
    {
        var before = await TestDatabase.SnapshotAsync();

        var (response, text) = await SendRawAsync(
            HttpMethod.Post, "/api/v1/students", "studentNumber=ST-600", "text/plain");

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        Assert.Equal("unsupported_media_type", ErrorCode(text));
        Assert.Equal(before, await TestDatabase.SnapshotAsync());
    }

    // ---- API-10: single borrow, rules that need real ids ----

    [Fact, Trait("TC6", "E03")]
    public async Task E03_Borrow_with_an_expected_date_that_is_not_after_checkout_is_a_400()
    {
        var s4 = await StudentIdAsync("ST-400");
        var e2 = await EquipmentIdAsync("EQ-002");
        var before = await TestDatabase.SnapshotAsync();

        foreach (var badDate in new[] { D, D.AddDays(-1) })           // same day, and in the past (BR-11)
        {
            var response = await BorrowAsync(s4, e2, badDate);
            var body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            AssertSafeError(body);
            Assert.Equal("validation_failed", ErrorCode(body));
        }

        Assert.Equal(before, await TestDatabase.SnapshotAsync());
    }

    [Fact, Trait("TC6", "E03")]
    public async Task E03_Borrow_business_conflicts_are_409_rule_violations()
    {
        var s2 = await StudentIdAsync("ST-200");   // ineligible
        var s4 = await StudentIdAsync("ST-400");
        var e1 = await EquipmentIdAsync("EQ-001"); // already borrowed
        var e2 = await EquipmentIdAsync("EQ-002");
        var before = await TestDatabase.SnapshotAsync();

        foreach (var (student, item) in new[] { (s2, e2), (s4, e1) })
        {
            var response = await BorrowAsync(student, item, D.AddDays(2));
            var body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            AssertSafeError(body);
            Assert.Equal("rule_violation", ErrorCode(body));
        }

        Assert.Equal(before, await TestDatabase.SnapshotAsync());
    }

    // ---- API-11: checkout request validation ----

    [Fact, Trait("TC6", "E03")]
    public async Task E03_Checkout_requests_that_are_malformed_are_a_400()
    {
        var s4 = await StudentIdAsync("ST-400");
        var e2 = await EquipmentIdAsync("EQ-002");
        var e6 = await EquipmentIdAsync("EQ-006");
        var before = await TestDatabase.SnapshotAsync();

        var cases = new (string Name, HttpResponseMessage Response)[]
        {
            ("one item",        await CheckoutAsync(s4, new[] { e2 }, D.AddDays(2))),
            ("no items",        await CheckoutAsync(s4, Array.Empty<int>(), D.AddDays(2))),
            ("duplicate items", await CheckoutAsync(s4, new[] { e2, e2 }, D.AddDays(2))),
            ("date = D",        await CheckoutAsync(s4, new[] { e2, e6 }, D)),
            ("date in past",    await CheckoutAsync(s4, new[] { e2, e6 }, D.AddDays(-3)))
        };

        foreach (var (name, response) in cases)
        {
            var body = await response.Content.ReadAsStringAsync();
            Assert.True(response.StatusCode == HttpStatusCode.BadRequest, $"'{name}' returned {(int)response.StatusCode}.");
            AssertSafeError(body);
            Assert.Equal("validation_failed", ErrorCode(body));
        }

        Assert.Equal(before, await TestDatabase.SnapshotAsync());
    }

    [Fact, Trait("TC6", "E03")]
    public async Task E03_Checkout_requests_that_break_a_business_rule_are_a_409_and_a_missing_item_is_a_404()
    {
        var s2 = await StudentIdAsync("ST-200");   // ineligible
        var s4 = await StudentIdAsync("ST-400");
        var e1 = await EquipmentIdAsync("EQ-001"); // unavailable
        var e2 = await EquipmentIdAsync("EQ-002");
        var e6 = await EquipmentIdAsync("EQ-006");
        var before = await TestDatabase.SnapshotAsync();

        var unavailable = await CheckoutAsync(s4, new[] { e1, e2 }, D.AddDays(2));
        Assert.Equal(HttpStatusCode.Conflict, unavailable.StatusCode);
        Assert.Equal("rule_violation", ErrorCode(await unavailable.Content.ReadAsStringAsync()));

        var ineligible = await CheckoutAsync(s2, new[] { e2, e6 }, D.AddDays(2));
        Assert.Equal(HttpStatusCode.Conflict, ineligible.StatusCode);
        Assert.Equal("rule_violation", ErrorCode(await ineligible.Content.ReadAsStringAsync()));

        var missingItem = await CheckoutAsync(s4, new[] { e2, 999_999 }, D.AddDays(2));
        Assert.Equal(HttpStatusCode.NotFound, missingItem.StatusCode);
        Assert.Equal("not_found", ErrorCode(await missingItem.Content.ReadAsStringAsync()));

        Assert.Equal(before, await TestDatabase.SnapshotAsync());      // none of the three wrote anything
    }
}
