namespace EquipmentBorrowing.Tests.Infrastructure;

// Test doubles for the HTTP transport. WHAT EACH CAN AND CANNOT PROVE is stated on every class:
// a double proves how the CLIENT reacts; it proves nothing about PostgreSQL or the real network.

/// <summary>
/// Fails every request like a dead network: no HTTP response ever exists.
/// CAN prove: the client maps "no response" to Unreachable and does not retry.
/// CANNOT prove: anything about real sockets, or what the server did.
/// </summary>
public sealed class ThrowingHandler : HttpMessageHandler
{
    private readonly Func<Exception> _exception;
    private int _calls;

    public ThrowingHandler(Func<Exception> exception) => _exception = exception;

    public int Calls => Volatile.Read(ref _calls);

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _calls);
        throw _exception();
    }
}

/// <summary>Never answers until it is cancelled. CAN prove the client's timeout handling only.</summary>
public sealed class HangingHandler : HttpMessageHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.Infinite, cancellationToken);
        return new HttpResponseMessage();
    }
}

/// <summary>
/// Passes everything through to the REAL API and only records what happened.
/// CAN prove: how many requests the client really sent (so, "no automatic retry") and what came back.
/// </summary>
public sealed class CountingHandler : DelegatingHandler
{
    private int _requests;

    public int Requests => Volatile.Read(ref _requests);
    public List<int> StatusCodes { get; } = new();

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _requests);
        var response = await base.SendAsync(request, cancellationToken);
        lock (StatusCodes) StatusCodes.Add((int)response.StatusCode);
        return response;
    }
}

/// <summary>
/// Sends the request to the REAL API (it is processed and committed in PostgreSQL), then throws as if the
/// response had been lost on the way back.
/// CAN prove: the client reports "may or may not have been processed" while the data really IS committed,
/// so only a fresh database read can settle the truth. CANNOT prove: any real network behaviour.
/// </summary>
public sealed class LoseResponseHandler : DelegatingHandler
{
    private int _requests;

    public int Requests => Volatile.Read(ref _requests);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _requests);
        using var response = await base.SendAsync(request, cancellationToken);
        throw new HttpRequestException("simulated: the connection was lost before the response arrived");
    }
}
