using System.Net;
using System.Net.Http.Json;
using EquipmentBorrowing.Api.Contracts;
using EquipmentBorrowing.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace EquipmentBorrowing.Tests.UseCases;

/// <summary>
/// TC6-E08 Fresh connections and restart (UC-03, 05, 12, 15; BR-13, 14, 19; API-03, 07, 10, 12, 13).
///
///   E08 (automated)  create student + equipment, issue, return; every check reads PostgreSQL through a
///                    NEW EF context and a NEW physical connection, never the API's own context.
///   E08 (restart)    INTEGRATION HARNESS: the actual API runs as a separate process (see ApiProcess.cs),
///                    is stopped and started again, and the records are retrieved over HTTP and compared
///                    with a fresh PostgreSQL read.
/// </summary>
[Collection("Api")]
public sealed class PersistenceTests : ApiTestBase
{
    public PersistenceTests(ApiFactory factory) : base(factory) { }

    [Fact, Trait("TC6", "E08")]
    public async Task E08_Created_and_issued_records_are_visible_through_fresh_contexts_and_connections()
    {
        // The API creates and disposes its own DbContext inside each request, so nothing is cached here.
        var student = await Client.PostAsJsonAsync("/api/v1/students",
            new { studentNumber = "ST-500", fullName = "Eli Tan", contact = (string?)null }, Json);
        var equipment = await Client.PostAsJsonAsync("/api/v1/equipment",
            new { equipmentCode = "EQ-008", name = "Webcam", description = (string?)null }, Json);
        Assert.Equal(HttpStatusCode.Created, student.StatusCode);
        Assert.Equal(HttpStatusCode.Created, equipment.StatusCode);
        var studentId = (await ReadAsync<StudentDto>(student)).Id;
        var equipmentId = (await ReadAsync<EquipmentDto>(equipment)).Id;

        var issued = await BorrowAsync(studentId, equipmentId, D.AddDays(2));
        Assert.Equal(HttpStatusCode.Created, issued.StatusCode);
        var borrowingId = (await ReadAsync<BorrowingDto>(issued)).Id;

        // Fresh EF context #1: a brand-new object graph straight from PostgreSQL.
        await using (var context = TestDatabase.NewContext())
        {
            Assert.Equal(5, await context.Students.CountAsync());
            Assert.Equal(8, await context.Equipment.CountAsync());
            Assert.Equal(5, await context.Borrowings.CountAsync());
            Assert.Equal(1, await context.Students.CountAsync(s => s.Id == studentId));
            Assert.Equal(1, await context.Equipment.CountAsync(e => e.Id == equipmentId));
            Assert.Equal(1, await context.Borrowings.CountAsync(b => b.Id == borrowingId));
        }

        // Fresh physical connection #1: the values, relationships and item state.
        Assert.Equal("ST-500", await TestDatabase.ScalarAsync<string>(
            "SELECT \"StudentNumber\" FROM \"Students\" WHERE \"Id\" = @id", ("id", studentId)));
        Assert.Equal("EQ-008", await TestDatabase.ScalarAsync<string>(
            "SELECT \"EquipmentCode\" FROM \"Equipment\" WHERE \"Id\" = @id", ("id", equipmentId)));
        Assert.Equal(1, await TestDatabase.ScalarAsync<long>(
            "SELECT COUNT(*) FROM \"Borrowings\" WHERE \"Id\" = @b AND \"StudentId\" = @s AND \"EquipmentId\" = @e AND \"Status\" = 'Active'",
            ("b", borrowingId), ("s", studentId), ("e", equipmentId)));
        Assert.False(await TestDatabase.IsAvailableAsync(equipmentId));
        Assert.True(await TestDatabase.IsAllowedToBorrowAsync(studentId));

        // Return it, then check AGAIN through other fresh connections: history retained, item free.
        Assert.Equal(HttpStatusCode.OK, (await ReturnAsync(borrowingId)).StatusCode);

        await using (var context = TestDatabase.NewContext())
        {
            Assert.Equal(5, await context.Borrowings.CountAsync());                       // retained, not deleted
            Assert.Equal(1, await context.Borrowings.CountAsync(b => b.Id == borrowingId));
        }

        Assert.Equal("Returned", await TestDatabase.BorrowingStatusAsync(borrowingId));
        Assert.Equal(Iso(D), await TestDatabase.ReturnedDateAsync(borrowingId));
        Assert.True(await TestDatabase.IsAvailableAsync(equipmentId));
        Assert.Equal(1, await TestDatabase.BorrowingCountForStudentAsync(studentId));
        Assert.Equal(0, await TestDatabase.ConsistencyViolationsAsync());
    }

    [Fact, Trait("TC6", "E08")]
    public async Task E08_Restart_the_actual_API_process_and_the_same_records_come_back()
    {
        string snapshotBeforeStop;
        int studentId, equipmentId, returnedId, activeId, e2Id;

        // ---- run 1: the actual API as its own process ----
        using var first = new ApiProcess();
        await first.StartAsync();

        using (var http = new HttpClient { BaseAddress = first.BaseAddress })
        {
            var s4 = (await (await http.GetAsync("api/v1/students?search=ST-400")).Content
                .ReadFromJsonAsync<List<StudentDto>>(Json))!.Single().Id;
            e2Id = (await (await http.GetAsync("api/v1/equipment")).Content
                .ReadFromJsonAsync<List<EquipmentDto>>(Json))!.Single(e => e.EquipmentCode == "EQ-002").Id;

            var student = await http.PostAsJsonAsync("api/v1/students",
                new { studentNumber = "ST-500", fullName = "Eli Tan", contact = (string?)null }, Json);
            var equipment = await http.PostAsJsonAsync("api/v1/equipment",
                new { equipmentCode = "EQ-008", name = "Webcam", description = (string?)null }, Json);
            Assert.Equal(HttpStatusCode.Created, student.StatusCode);
            Assert.Equal(HttpStatusCode.Created, equipment.StatusCode);
            studentId = (await student.Content.ReadFromJsonAsync<StudentDto>(Json))!.Id;
            equipmentId = (await equipment.Content.ReadFromJsonAsync<EquipmentDto>(Json))!.Id;

            // New student borrows the new item and returns it (returned history).
            var borrowed = await http.PostAsJsonAsync("api/v1/borrowings",
                new { studentId, equipmentId, expectedReturnDate = Iso(D.AddDays(2)) }, Json);
            Assert.Equal(HttpStatusCode.Created, borrowed.StatusCode);
            returnedId = (await borrowed.Content.ReadFromJsonAsync<BorrowingDto>(Json))!.Id;
            var returned = await http.PostAsJsonAsync($"api/v1/borrowings/{returnedId}/return", new { }, Json);
            Assert.Equal(HttpStatusCode.OK, returned.StatusCode);

            // S4 keeps an ACTIVE borrowing of E2 (active history).
            var active = await http.PostAsJsonAsync("api/v1/borrowings",
                new { studentId = s4, equipmentId = e2Id, expectedReturnDate = Iso(D.AddDays(2)) }, Json);
            Assert.Equal(HttpStatusCode.Created, active.StatusCode);
            activeId = (await active.Content.ReadFromJsonAsync<BorrowingDto>(Json))!.Id;
        }

        snapshotBeforeStop = await TestDatabase.SnapshotAsync();

        // ---- stop it: really gone ----
        first.Stop();
        using (var gone = new HttpClient { BaseAddress = first.BaseAddress, Timeout = TimeSpan.FromSeconds(5) })
            await Assert.ThrowsAnyAsync<HttpRequestException>(() => gone.GetAsync("api/v1/equipment"));

        // ---- run 2: a NEW process on a NEW port, same database ----
        using var second = new ApiProcess();
        await second.StartAsync();

        using (var http = new HttpClient { BaseAddress = second.BaseAddress })
        {
            var student = await http.GetAsync($"api/v1/students/{studentId}");
            Assert.Equal(HttpStatusCode.OK, student.StatusCode);
            var studentBody = (await student.Content.ReadFromJsonAsync<StudentDto>(Json))!;
            Assert.Equal("ST-500", studentBody.StudentNumber);
            Assert.True(studentBody.IsEligible);

            var equipment = await http.GetAsync($"api/v1/equipment/{equipmentId}");
            Assert.Equal(HttpStatusCode.OK, equipment.StatusCode);
            Assert.True((await equipment.Content.ReadFromJsonAsync<EquipmentDto>(Json))!.IsAvailable);   // returned => free

            var returned = await http.GetAsync($"api/v1/borrowings/{returnedId}");
            Assert.Equal(HttpStatusCode.OK, returned.StatusCode);
            var returnedBody = (await returned.Content.ReadFromJsonAsync<BorrowingDto>(Json))!;
            Assert.Equal("Returned", returnedBody.Status);          // returned history survived the restart
            Assert.Equal(D, returnedBody.ReturnedDate);
            Assert.Equal(studentId, returnedBody.StudentId);
            Assert.Equal(equipmentId, returnedBody.EquipmentId);

            var active = await http.GetAsync($"api/v1/borrowings/{activeId}");
            Assert.Equal(HttpStatusCode.OK, active.StatusCode);
            Assert.Equal("Active", (await active.Content.ReadFromJsonAsync<BorrowingDto>(Json))!.Status);

            var e2 = await http.GetAsync($"api/v1/equipment/{e2Id}");
            Assert.False((await e2.Content.ReadFromJsonAsync<EquipmentDto>(Json))!.IsAvailable);        // still on loan
        }

        // ---- the database itself is byte-for-byte what it was before the restart ----
        Assert.Equal(snapshotBeforeStop, await TestDatabase.SnapshotAsync());
        Assert.Equal(0, await TestDatabase.ConsistencyViolationsAsync());
    }
}
