using System.Net;
using System.Text.Json;
using EquipmentBorrowing.Api.Contracts;
using EquipmentBorrowing.Tests.Infrastructure;

namespace EquipmentBorrowing.Tests.UseCases;

/// <summary>TC6-U07 Student history (API-03, API-15) and TC6-U08 Equipment history (API-07, API-16).</summary>
[Collection("Api")]
public sealed class HistoryTests : ApiTestBase
{
    private const int MissingId = 999_999;

    public HistoryTests(ApiFactory factory) : base(factory) { }

    private static List<BorrowingDto> Parse(string body) =>
        JsonSerializer.Deserialize<List<BorrowingDto>>(body, Json)!;

    // ---------------- TC6-U07 ----------------

    [Fact, Trait("TC6", "U07")]
    public async Task U07_Student_S1_history_has_the_active_E1_and_returned_E3_records()
    {
        var before = await TestDatabase.SnapshotAsync();
        var s1 = await StudentIdAsync("ST-100");

        // API-03: the selected student itself.
        var student = await Client.GetAsync($"/api/v1/students/{s1}");
        Assert.Equal(HttpStatusCode.OK, student.StatusCode);
        var s1Dto = await ReadAsync<StudentDto>(student);
        Assert.Equal("ST-100", s1Dto.StudentNumber);
        Assert.Equal("Ada Cruz", s1Dto.FullName);
        Assert.Null(s1Dto.Contact);
        Assert.True(s1Dto.IsEligible);

        // API-15: the history.
        var response = await Client.GetAsync($"/api/v1/students/{s1}/borrowings");
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Raw JSON contract check on the active record (names, date format, null while active).
        using (var doc = JsonDocument.Parse(body))
        {
            var active = doc.RootElement.EnumerateArray()
                .Single(e => e.GetProperty("equipmentCode").GetString() == "EQ-001");

            foreach (var name in new[] { "id", "studentId", "equipmentId", "borrowedDate",
                                         "expectedReturnDate", "returnedDate", "status" })
                Assert.True(active.TryGetProperty(name, out _), $"JSON property '{name}' is missing.");

            Assert.Equal(JsonValueKind.Null, active.GetProperty("returnedDate").ValueKind);
            Assert.Equal("Active", active.GetProperty("status").GetString());
            Assert.Equal(Iso(D.AddDays(-9)), active.GetProperty("borrowedDate").GetString());
        }

        var history = Parse(body);
        Assert.Equal(2, history.Count);
        Assert.All(history, b => Assert.Equal(s1, b.StudentId));
        Assert.All(history, b => Assert.Equal("ST-100", b.StudentNumber));

        var e1 = history.Single(b => b.EquipmentCode == "EQ-001");
        Assert.Equal("Camera", e1.EquipmentName);
        Assert.Equal("Active", e1.Status);
        Assert.Equal(D.AddDays(-9), e1.BorrowedDate);
        Assert.Equal(D.AddDays(-1), e1.ExpectedReturnDate);
        Assert.Null(e1.ReturnedDate);

        var e3 = history.Single(b => b.EquipmentCode == "EQ-003");
        Assert.Equal("Projector", e3.EquipmentName);
        Assert.Equal("Returned", e3.Status);
        Assert.Equal(D.AddDays(-8), e3.BorrowedDate);
        Assert.Equal(D.AddDays(2), e3.ExpectedReturnDate);
        Assert.Equal(D.AddDays(-2), e3.ReturnedDate);

        // Fresh-connection cross-check: the API returned exactly the rows PostgreSQL holds.
        Assert.Equal(2, await TestDatabase.ScalarAsync<long>(
            "SELECT COUNT(*) FROM \"Borrowings\" WHERE \"StudentId\" = @id", ("id", s1)));

        Assert.Equal(before, await TestDatabase.SnapshotAsync());
    }

    [Fact, Trait("TC6", "U07")]
    public async Task U07_Histories_of_two_students_do_not_mix()
    {
        var s1 = await StudentIdAsync("ST-100");
        var s3 = await StudentIdAsync("ST-300");

        var s1History = Parse(await Client.GetStringAsync($"/api/v1/students/{s1}/borrowings"));
        var s3History = Parse(await Client.GetStringAsync($"/api/v1/students/{s3}/borrowings"));

        Assert.Equal(new[] { "EQ-004", "EQ-005" }, s3History.Select(b => b.EquipmentCode).OrderBy(c => c).ToArray());
        Assert.All(s3History, b => Assert.Equal("Active", b.Status));
        Assert.All(s3History, b => Assert.Equal(s3, b.StudentId));

        Assert.Empty(s1History.Select(b => b.Id).Intersect(s3History.Select(b => b.Id)));
    }

    [Fact, Trait("TC6", "U07")]
    public async Task U07_Student_S4_without_history_returns_200_and_an_empty_array()
    {
        var s4 = await StudentIdAsync("ST-400");

        var response = await Client.GetAsync($"/api/v1/students/{s4}/borrowings");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);   // existing student, no history: NOT a 404
        Assert.Equal("[]", (await response.Content.ReadAsStringAsync()).Trim());
    }

    [Fact, Trait("TC6", "U07")]
    public async Task U07_Missing_student_returns_404_with_a_safe_error_body()
    {
        var history = await Client.GetAsync($"/api/v1/students/{MissingId}/borrowings");
        Assert.Equal(HttpStatusCode.NotFound, history.StatusCode);
        AssertSafeError(await history.Content.ReadAsStringAsync());

        var student = await Client.GetAsync($"/api/v1/students/{MissingId}");
        Assert.Equal(HttpStatusCode.NotFound, student.StatusCode);
        AssertSafeError(await student.Content.ReadAsStringAsync());
    }

    // ---------------- TC6-U08 ----------------

    [Fact, Trait("TC6", "U08")]
    public async Task U08_Equipment_E1_history_has_one_active_record_for_S1()
    {
        var before = await TestDatabase.SnapshotAsync();
        var e1 = await EquipmentIdAsync("EQ-001");

        // API-07: the selected item itself.
        var item = await Client.GetAsync($"/api/v1/equipment/{e1}");
        Assert.Equal(HttpStatusCode.OK, item.StatusCode);
        var e1Dto = await ReadAsync<EquipmentDto>(item);
        Assert.Equal("EQ-001", e1Dto.EquipmentCode);
        Assert.Equal("Camera", e1Dto.Name);
        Assert.False(e1Dto.IsAvailable);

        // API-16: the history.
        var response = await Client.GetAsync($"/api/v1/equipment/{e1}/borrowings");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var history = await ReadAsync<List<BorrowingDto>>(response);

        var only = Assert.Single(history);
        Assert.Equal(e1, only.EquipmentId);
        Assert.Equal("ST-100", only.StudentNumber);
        Assert.Equal("Ada Cruz", only.StudentName);
        Assert.Equal("Active", only.Status);
        Assert.Equal(D.AddDays(-9), only.BorrowedDate);
        Assert.Equal(D.AddDays(-1), only.ExpectedReturnDate);
        Assert.Null(only.ReturnedDate);

        Assert.Equal(1, await TestDatabase.ScalarAsync<long>(
            "SELECT COUNT(*) FROM \"Borrowings\" WHERE \"EquipmentId\" = @id", ("id", e1)));

        Assert.Equal(before, await TestDatabase.SnapshotAsync());
    }

    [Fact, Trait("TC6", "U08")]
    public async Task U08_Equipment_E3_history_has_one_returned_record_for_S1()
    {
        var e3 = await EquipmentIdAsync("EQ-003");

        var history = await ReadAsync<List<BorrowingDto>>(
            await Client.GetAsync($"/api/v1/equipment/{e3}/borrowings"));

        var only = Assert.Single(history);
        Assert.Equal(e3, only.EquipmentId);
        Assert.Equal("ST-100", only.StudentNumber);
        Assert.Equal("Returned", only.Status);
        Assert.Equal(D.AddDays(-8), only.BorrowedDate);
        Assert.Equal(D.AddDays(2), only.ExpectedReturnDate);
        Assert.Equal(D.AddDays(-2), only.ReturnedDate);
    }

    [Fact, Trait("TC6", "U08")]
    public async Task U08_Equipment_E2_without_history_returns_200_and_an_empty_array()
    {
        var e2 = await EquipmentIdAsync("EQ-002");

        var response = await Client.GetAsync($"/api/v1/equipment/{e2}/borrowings");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);   // existing item, no history: NOT a 404
        Assert.Equal("[]", (await response.Content.ReadAsStringAsync()).Trim());
    }

    [Fact, Trait("TC6", "U08")]
    public async Task U08_Missing_equipment_returns_404_with_a_safe_error_body()
    {
        var history = await Client.GetAsync($"/api/v1/equipment/{MissingId}/borrowings");
        Assert.Equal(HttpStatusCode.NotFound, history.StatusCode);
        AssertSafeError(await history.Content.ReadAsStringAsync());

        var item = await Client.GetAsync($"/api/v1/equipment/{MissingId}");
        Assert.Equal(HttpStatusCode.NotFound, item.StatusCode);
        AssertSafeError(await item.Content.ReadAsStringAsync());
    }
}
