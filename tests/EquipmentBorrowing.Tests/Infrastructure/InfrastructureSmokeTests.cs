using System.Net;
using System.Net.Http.Json;
using EquipmentBorrowing.Api.Contracts;

// Tests share ONE PostgreSQL test database and some use process-wide environment variables
// (fault injection), so they must never run in parallel.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace EquipmentBorrowing.Tests.Infrastructure;

/// <summary>
/// NOT a TC6 case. These prove the test harness itself is trustworthy before any TC6 test
/// relies on it (so they do not appear in the traceability matrix).
/// </summary>
[Collection("Api")]
public sealed class InfrastructureSmokeTests : ApiTestBase
{
    public InfrastructureSmokeTests(ApiFactory factory) : base(factory) { }

    [Fact]
    public async Task Reset_inserts_the_lab_dataset()
    {
        Assert.Equal(4, await TestDatabase.CountAsync("Students"));
        Assert.Equal(7, await TestDatabase.CountAsync("Equipment"));
        Assert.Equal(4, await TestDatabase.CountAsync("Borrowings"));
        Assert.Equal(3, await TestDatabase.ScalarAsync<long>(
            "SELECT COUNT(*) FROM \"Borrowings\" WHERE \"Status\" = 'Active'"));
        Assert.Equal(0, await TestDatabase.ConsistencyViolationsAsync());
    }

    [Fact]
    public async Task Reset_is_repeatable()
    {
        var first = await TestDatabase.SnapshotAsync();
        await TestDatabase.ResetAsync();
        var second = await TestDatabase.SnapshotAsync();

        Assert.Equal(first, second);
    }

    [Fact]
    public async Task Real_http_pipeline_reads_the_same_database()
    {
        var response = await Client.GetAsync("/api/v1/equipment");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = await response.Content.ReadFromJsonAsync<List<EquipmentDto>>(Json);
        Assert.Equal(7, items!.Count);
    }

    [Fact]
    public async Task Ids_are_resolved_from_the_api()
    {
        var s1 = await StudentIdAsync("ST-100");
        var e1 = await EquipmentIdAsync("EQ-001");

        Assert.True(s1 > 0);
        Assert.True(e1 > 0);
    }
}
