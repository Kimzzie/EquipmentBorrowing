using System.Net;
using EquipmentBorrowing.Api.Contracts;
using EquipmentBorrowing.Tests.Infrastructure;

namespace EquipmentBorrowing.Tests.UseCases;

/// <summary>TC6-E06 Optional and protected fields (UC-13, UC-16; BR-02, 03, 06, 07, 14, 16; API-05, API-09).</summary>
[Collection("Api")]
public sealed class ProtectedFieldTests : ApiTestBase
{
    public ProtectedFieldTests(ApiFactory factory) : base(factory) { }

    // ---------------- optional fields can be cleared ----------------

    [Fact, Trait("TC6", "E06")]
    public async Task E06_Null_contact_clears_the_optional_student_field()
    {
        var s1 = await StudentIdAsync("ST-100");
        Assert.Equal(HttpStatusCode.OK, (await UpdateStudentAsync(s1, "ST-100", "Ada Cruz", "0917-555-0100")).StatusCode);
        Assert.Equal("0917-555-0100", await TestDatabase.ScalarAsync<string>(
            "SELECT \"Contact\" FROM \"Students\" WHERE \"Id\" = @id", ("id", s1)));

        var response = await UpdateStudentAsync(s1, "ST-100", "Ada Cruz", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null((await ReadAsync<StudentDto>(response)).Contact);
        Assert.Equal(1, await TestDatabase.ScalarAsync<long>(
            "SELECT COUNT(*) FROM \"Students\" WHERE \"Id\" = @id AND \"Contact\" IS NULL", ("id", s1)));
        Assert.Equal(2, await TestDatabase.BorrowingCountForStudentAsync(s1));   // history untouched
    }

    [Fact, Trait("TC6", "E06")]
    public async Task E06_A_blank_contact_also_clears_it_on_update()
    {
        // BR-03: "A blank optional contact clears it."
        var s1 = await StudentIdAsync("ST-100");
        await UpdateStudentAsync(s1, "ST-100", "Ada Cruz", "0917-555-0100");

        var response = await UpdateStudentAsync(s1, "ST-100", "Ada Cruz", "   ");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null((await ReadAsync<StudentDto>(response)).Contact);
        Assert.Equal(1, await TestDatabase.ScalarAsync<long>(
            "SELECT COUNT(*) FROM \"Students\" WHERE \"Id\" = @id AND \"Contact\" IS NULL", ("id", s1)));
    }

    [Fact, Trait("TC6", "E06")]
    public async Task E06_Null_description_clears_the_item_description_without_touching_availability_or_history()
    {
        var e1 = await EquipmentIdAsync("EQ-001");
        await UpdateEquipmentAsync(e1, "EQ-001", "Camera", "DSLR body");

        var response = await UpdateEquipmentAsync(e1, "EQ-001", "Camera", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await ReadAsync<EquipmentDto>(response);
        Assert.Null(updated.Description);
        Assert.False(updated.IsAvailable);

        Assert.Equal(1, await TestDatabase.ScalarAsync<long>(
            "SELECT COUNT(*) FROM \"Equipment\" WHERE \"Id\" = @id AND \"Description\" IS NULL", ("id", e1)));
        Assert.False(await TestDatabase.IsAvailableAsync(e1));
        Assert.Equal(1, await TestDatabase.ScalarAsync<long>(
            "SELECT COUNT(*) FROM \"Borrowings\" WHERE \"EquipmentId\" = @id AND \"Status\" = 'Active'", ("id", e1)));
    }

    // ---------------- required fields cannot be blanked ----------------

    [Fact, Trait("TC6", "E06")]
    public async Task E06_Blank_required_fields_are_rejected_with_400_and_change_nothing()
    {
        var s1 = await StudentIdAsync("ST-100");
        var e2 = await EquipmentIdAsync("EQ-002");
        var before = await TestDatabase.SnapshotAsync();

        var cases = new (string Name, HttpResponseMessage Response)[]
        {
            ("student number blank", await UpdateStudentAsync(s1, "   ", "Ada Cruz", null)),
            ("student name blank",   await UpdateStudentAsync(s1, "ST-100", "", null)),
            ("student name null",    await UpdateStudentAsync(s1, "ST-100", null, null)),
            ("equipment code blank", await UpdateEquipmentAsync(e2, "  ", "Laptop", null)),
            ("equipment name blank", await UpdateEquipmentAsync(e2, "EQ-002", "", null)),
            ("equipment name null",  await UpdateEquipmentAsync(e2, "EQ-002", null, null))
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

    [Fact, Trait("TC6", "E06")]
    public async Task E06_A_duplicate_identifier_on_update_is_a_409_and_changes_nothing()
    {
        var s4 = await StudentIdAsync("ST-400");
        var e2 = await EquipmentIdAsync("EQ-002");
        var before = await TestDatabase.SnapshotAsync();

        var student = await UpdateStudentAsync(s4, "st-100", "Dani Ong", null);
        var item = await UpdateEquipmentAsync(e2, " eq-001 ", "Laptop", null);

        Assert.Equal(HttpStatusCode.Conflict, student.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, item.StatusCode);
        Assert.Equal("duplicate", ErrorCode(await student.Content.ReadAsStringAsync()));
        Assert.Equal("duplicate", ErrorCode(await item.Content.ReadAsStringAsync()));
        Assert.Equal(before, await TestDatabase.SnapshotAsync());
    }

    // ---------------- protected fields cannot be edited ----------------

    [Fact, Trait("TC6", "E06")]
    public async Task E06_Availability_sent_in_an_equipment_update_is_rejected_with_400()
    {
        var e1 = await EquipmentIdAsync("EQ-001");
        var before = await TestDatabase.SnapshotAsync();

        var (response, body) = await SendRawAsync(HttpMethod.Put, $"/api/v1/equipment/{e1}",
            "{\"equipmentCode\":\"EQ-001\",\"name\":\"Camera\",\"isAvailable\":true}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        AssertSafeError(body);
        Assert.Equal("validation_failed", ErrorCode(body));

        Assert.False(await TestDatabase.IsAvailableAsync(e1));        // still on loan
        Assert.Equal(before, await TestDatabase.SnapshotAsync());
        Assert.Equal(0, await TestDatabase.ConsistencyViolationsAsync());
    }

    [Theory, Trait("TC6", "E06")]
    [InlineData("{\"studentNumber\":\"ST-100\",\"fullName\":\"Ada Cruz\",\"isEligible\":false}")]
    [InlineData("{\"id\":99,\"studentNumber\":\"ST-100\",\"fullName\":\"Ada Cruz\"}")]
    public async Task E06_Eligibility_and_identity_sent_in_a_student_update_are_rejected_with_400(string json)
    {
        var s1 = await StudentIdAsync("ST-100");
        var before = await TestDatabase.SnapshotAsync();

        var (response, body) = await SendRawAsync(HttpMethod.Put, $"/api/v1/students/{s1}", json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("validation_failed", ErrorCode(body));
        Assert.True(await TestDatabase.IsAllowedToBorrowAsync(s1));   // eligibility changes only via UC-14
        Assert.Equal(before, await TestDatabase.SnapshotAsync());
    }

    [Theory, Trait("TC6", "E06")]
    [InlineData("{\"equipmentCode\":\"EQ-002\",\"name\":\"Laptop\",\"id\":99}")]
    [InlineData("{\"equipmentCode\":\"EQ-002\",\"name\":\"Laptop\",\"status\":\"Retired\"}")]
    public async Task E06_Identity_and_status_sent_in_an_equipment_update_are_rejected_with_400(string json)
    {
        var e2 = await EquipmentIdAsync("EQ-002");
        var before = await TestDatabase.SnapshotAsync();

        var (response, body) = await SendRawAsync(HttpMethod.Put, $"/api/v1/equipment/{e2}", json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("validation_failed", ErrorCode(body));
        Assert.Equal(before, await TestDatabase.SnapshotAsync());
    }
}
