using System.Net.Sockets;
using EquipmentBorrowing.Desktop.Api;
using EquipmentBorrowing.Tests.Infrastructure;

namespace EquipmentBorrowing.Tests.UseCases;

/// <summary>
/// TC6-E07 Transport and uncertain outcome (UC-03, UC-06; BR-18, 19; API-10, API-11).
/// These tests drive the REAL desktop HTTP client (EquipmentApiClient, compiled in unmodified).
///
///   E07a  no HTTP response at all     -> ApiFailure.Unreachable      (test double AND a real closed port)
///   E07b  a reachable API answers 500 -> ApiFailure.Server           (real API + real PostgreSQL + fault seam)
///   E07c  the outcome is unknown      -> never retried, state verified in PostgreSQL
///
/// Each double's limits are written on its class in TransportDoubles.cs.
/// </summary>
[Collection("Api")]
public sealed class TransportTests : ApiTestBase
{
    private readonly ApiFactory _factory;

    public TransportTests(ApiFactory factory) : base(factory) => _factory = factory;

    private CheckoutRequest CheckoutOf(int student, params int[] items) =>
        new(student, items.ToList(), D.AddDays(2));

    // ---------------- E07a: no HTTP response ----------------

    [Fact, Trait("TC6", "E07a")]
    public async Task E07a_Double_a_dead_network_is_reported_as_Unreachable_and_the_write_is_not_retried()
    {
        var network = new ThrowingHandler(() => new HttpRequestException("simulated: connection refused"));
        var client = new EquipmentApiClient(new HttpClient(network) { BaseAddress = new Uri("http://api.invalid/") });

        var result = await client.CheckoutAsync(CheckoutOf(1, 2, 3));

        Assert.False(result.IsSuccess);
        Assert.Equal(ApiFailure.Unreachable, result.Failure);       // NOT Server: no HTTP status exists
        Assert.Null(result.Code);                                    // there is no error body to read a code from
        Assert.Contains("Cannot reach", result.Message);
        Assert.Contains("may or may not have been processed", result.Message);   // a WRITE carries the warning
        Assert.Equal(1, network.Calls);                              // exactly one attempt: no automatic retry
    }

    [Fact, Trait("TC6", "E07a")]
    public async Task E07a_Double_a_failed_read_is_Unreachable_without_the_write_warning()
    {
        var network = new ThrowingHandler(() => new HttpRequestException("simulated: connection refused"));
        var client = new EquipmentApiClient(new HttpClient(network) { BaseAddress = new Uri("http://api.invalid/") });

        var result = await client.GetEquipmentAsync(availableOnly: false);

        Assert.Equal(ApiFailure.Unreachable, result.Failure);
        Assert.DoesNotContain("may or may not", result.Message);    // nothing was written, so nothing is uncertain
        Assert.Equal(1, network.Calls);
    }

    [Fact, Trait("TC6", "E07a")]
    public async Task E07a_A_real_closed_port_gives_a_genuine_connection_refusal_reported_as_Unreachable()
    {
        // No double: a real socket to a port nobody listens on (found by binding and releasing one).
        var port = ApiProcess.FreePort();
        using var http = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}/"), Timeout = TimeSpan.FromSeconds(10) };
        var client = new EquipmentApiClient(http);

        var result = await client.GetEquipmentAsync(availableOnly: false);

        Assert.Equal(ApiFailure.Unreachable, result.Failure);
        Assert.Contains("Cannot reach", result.Message);
        Assert.Contains($"127.0.0.1:{port}", result.Message);        // tells staff WHICH address failed
    }

    [Fact, Trait("TC6", "E07a")]
    public async Task E07a_Double_a_server_that_never_answers_times_out_as_Unreachable()
    {
        var client = new EquipmentApiClient(new HttpClient(new HangingHandler())
        {
            BaseAddress = new Uri("http://api.invalid/"),
            Timeout = TimeSpan.FromMilliseconds(300)
        });

        var result = await client.BorrowAsync(new BorrowRequest(1, 2, D.AddDays(2)));

        Assert.Equal(ApiFailure.Unreachable, result.Failure);
        Assert.Contains("did not respond in time", result.Message);
        Assert.Contains("may or may not have been processed", result.Message);
    }

    // ---------------- E07b: a received 500 ----------------

    [Fact, Trait("TC6", "E07b")]
    public async Task E07b_A_reachable_API_that_fails_is_a_Server_error_distinct_from_Unreachable_and_nothing_is_saved()
    {
        var counter = new CountingHandler();
        var client = new EquipmentApiClient(_factory.CreateDefaultClient(counter));
        var s4 = await StudentIdAsync("ST-400");
        var e2 = await EquipmentIdAsync("EQ-002");
        var e6 = await EquipmentIdAsync("EQ-006");
        var baseline = await TestDatabase.SnapshotAsync();

        ApiResult<CheckoutResponse> result;
        using (FaultScope.BeforeCommit())
            result = await client.CheckoutAsync(CheckoutOf(s4, e2, e6));

        // An HTTP response WAS received (unlike E07a), and it was a 500.
        Assert.Equal(new[] { 500 }, counter.StatusCodes);
        Assert.Equal(ApiFailure.Server, result.Failure);
        Assert.NotEqual(ApiFailure.Unreachable, result.Failure);
        Assert.Equal("database_error", result.Code);
        Assert.Contains("The service is running but reported an error", result.Message);

        // Safe feedback: no credentials, no stack trace.
        Assert.DoesNotContain("Password", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("   at ", result.Message);

        // One request only (no retry), and a known failure leaves no partial change (fresh read).
        Assert.Equal(1, counter.Requests);
        Assert.Equal(baseline, await TestDatabase.SnapshotAsync());
    }

    // ---------------- E07c: unknown outcome ----------------

    [Fact, Trait("TC6", "E07c")]
    public async Task E07c_An_unknown_commit_outcome_is_reported_not_retried_and_settled_only_by_reading_PostgreSQL()
    {
        var counter = new CountingHandler();
        var client = new EquipmentApiClient(_factory.CreateDefaultClient(counter));
        var s4 = await StudentIdAsync("ST-400");
        var e2 = await EquipmentIdAsync("EQ-002");
        var e6 = await EquipmentIdAsync("EQ-006");

        // The commit SUCCEEDS inside PostgreSQL, then the acknowledgement is "lost".
        ApiResult<CheckoutResponse> result;
        using (FaultScope.AfterCommit())
            result = await client.CheckoutAsync(CheckoutOf(s4, e2, e6));
        var requestsMade = counter.Requests;

        // 1. The client was told the outcome is UNKNOWN, not "failed" and not "succeeded".
        Assert.False(result.IsSuccess);
        Assert.Equal(ApiFailure.Server, result.Failure);
        Assert.Equal("commit_outcome_unknown", result.Code);
        Assert.Contains("not known whether", result.Message);
        Assert.Equal(new[] { 500 }, counter.StatusCodes);

        // 2. NO automatic retry happened.
        Assert.Equal(1, requestsMade);

        // 3. The truth is only visible in PostgreSQL: here the commit really did happen.
        Assert.Equal(2, await TestDatabase.ActiveCountForStudentAsync(s4));
        Assert.False(await TestDatabase.IsAvailableAsync(e2));
        Assert.False(await TestDatabase.IsAvailableAsync(e6));
        Assert.Equal(0, await TestDatabase.ConsistencyViolationsAsync());   // all-or-nothing held

        // 4. "Refresh before deciding": the client can now see the records.
        var refreshed = await client.GetStudentBorrowingsAsync(s4);
        Assert.True(refreshed.IsSuccess);
        Assert.Equal(2, refreshed.Value!.Count(b => b.Status == "Active"));

        // 5. A blind retry would be safe anyway: it is rejected, so no duplicate is created.
        var blindRetry = await client.CheckoutAsync(CheckoutOf(s4, e2, e6));
        Assert.Equal(ApiFailure.Rejected, blindRetry.Failure);
        Assert.Equal(2, await TestDatabase.ActiveCountForStudentAsync(s4));
    }

    [Fact, Trait("TC6", "E07c")]
    public async Task E07c_A_response_lost_in_transit_is_Unreachable_with_a_warning_while_the_data_really_is_saved()
    {
        // Double: the request reaches the REAL API and is committed, but the response never arrives.
        var lossy = new LoseResponseHandler();
        var client = new EquipmentApiClient(_factory.CreateDefaultClient(lossy));
        var s4 = await StudentIdAsync("ST-400");
        var e2 = await EquipmentIdAsync("EQ-002");

        var result = await client.BorrowAsync(new BorrowRequest(s4, e2, D.AddDays(2)));

        Assert.Equal(ApiFailure.Unreachable, result.Failure);
        Assert.Contains("may or may not have been processed", result.Message);
        Assert.Equal(1, lossy.Requests);                              // not retried

        // The client cannot know, but PostgreSQL can: the borrowing WAS saved. This is exactly why the
        // message tells staff to refresh and check instead of pressing the button again.
        Assert.Equal(1, await TestDatabase.ActiveCountForStudentAsync(s4));
        Assert.False(await TestDatabase.IsAvailableAsync(e2));
    }
}
