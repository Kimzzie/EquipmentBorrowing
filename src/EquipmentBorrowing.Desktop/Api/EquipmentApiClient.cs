using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace EquipmentBorrowing.Desktop.Api;

public sealed class EquipmentApiClient : IEquipmentApi
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web); // camelCase
    private readonly HttpClient _http;

    public EquipmentApiClient(HttpClient http) => _http = http;

    // ---------- equipment ----------
    public Task<ApiResult<IReadOnlyList<EquipmentDto>>> GetEquipmentAsync(bool availableOnly, CancellationToken ct = default)
        => SendAsync<IReadOnlyList<EquipmentDto>>(HttpMethod.Get,
            availableOnly ? "api/v1/equipment?available=true" : "api/v1/equipment", null, ct);

    public Task<ApiResult<EquipmentDto>> RegisterEquipmentAsync(EquipmentRequest request)
        => SendAsync<EquipmentDto>(HttpMethod.Post, "api/v1/equipment", request, CancellationToken.None);

    public Task<ApiResult<EquipmentDto>> UpdateEquipmentAsync(int id, EquipmentRequest request)
        => SendAsync<EquipmentDto>(HttpMethod.Put, $"api/v1/equipment/{id}", request, CancellationToken.None);

    // ---------- students ----------
    public Task<ApiResult<IReadOnlyList<StudentDto>>> GetStudentsAsync(string? search, CancellationToken ct = default)
        => SendAsync<IReadOnlyList<StudentDto>>(HttpMethod.Get,
            string.IsNullOrWhiteSpace(search) ? "api/v1/students" : $"api/v1/students?search={Uri.EscapeDataString(search.Trim())}",
            null, ct);

    public Task<ApiResult<StudentDto>> RegisterStudentAsync(StudentRequest request)
        => SendAsync<StudentDto>(HttpMethod.Post, "api/v1/students", request, CancellationToken.None);

    public Task<ApiResult<StudentDto>> UpdateStudentAsync(int id, StudentRequest request)
        => SendAsync<StudentDto>(HttpMethod.Put, $"api/v1/students/{id}", request, CancellationToken.None);

    public Task<ApiResult<StudentDto>> ChangeEligibilityAsync(int id, bool isEligible)
        => SendAsync<StudentDto>(HttpMethod.Patch, $"api/v1/students/{id}/eligibility",
            new EligibilityRequest(isEligible), CancellationToken.None);

    // ---------- borrowing ----------
    public Task<ApiResult<BorrowingDto>> BorrowAsync(BorrowRequest request)
        => SendAsync<BorrowingDto>(HttpMethod.Post, "api/v1/borrowings", request, CancellationToken.None);

    public Task<ApiResult<CheckoutResponse>> CheckoutAsync(CheckoutRequest request)
        => SendAsync<CheckoutResponse>(HttpMethod.Post, "api/v1/checkout-requests", request, CancellationToken.None);

    public Task<ApiResult<BorrowingDto>> ReturnAsync(int borrowingId)
        => SendAsync<BorrowingDto>(HttpMethod.Post, $"api/v1/borrowings/{borrowingId}/return",
            new { }, CancellationToken.None); // "{}" body, so Content-Type is always present

    public Task<ApiResult<IReadOnlyList<BorrowingDto>>> SearchBorrowingsAsync(
        string? search, string status, string sort, string direction, CancellationToken ct = default)
    {
        var query = $"status={status}&sort={sort}&direction={direction}";
        if (!string.IsNullOrWhiteSpace(search))
            query += $"&search={Uri.EscapeDataString(search.Trim())}";

        return SendAsync<IReadOnlyList<BorrowingDto>>(HttpMethod.Get, $"api/v1/borrowings?{query}", null, ct);
    }

    public Task<ApiResult<IReadOnlyList<BorrowingDto>>> GetStudentBorrowingsAsync(int studentId, CancellationToken ct = default)
        => SendAsync<IReadOnlyList<BorrowingDto>>(HttpMethod.Get, $"api/v1/students/{studentId}/borrowings", null, ct);

    public Task<ApiResult<IReadOnlyList<BorrowingDto>>> GetEquipmentBorrowingsAsync(int equipmentId, CancellationToken ct = default)
        => SendAsync<IReadOnlyList<BorrowingDto>>(HttpMethod.Get, $"api/v1/equipment/{equipmentId}/borrowings", null, ct);

    // ---------- the one place that talks HTTP ----------
    private async Task<ApiResult<T>> SendAsync<T>(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        var isWrite = method != HttpMethod.Get;

        try
        {
            using var request = new HttpRequestMessage(method, path);
            if (body is not null)
                request.Content = JsonContent.Create(body, body.GetType(), options: Json);

            using var response = await _http.SendAsync(request, ct);

            if (response.IsSuccessStatusCode)
            {
                var value = await response.Content.ReadFromJsonAsync<T>(Json, ct);
                return value is null
                    ? ApiResult<T>.Fail(ApiFailure.Unexpected, null, "The service returned an empty response.")
                    : ApiResult<T>.Ok(value);
            }

            return await ReadFailureAsync<T>(response, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw; // the caller cancelled (a newer search replaced this one): not an error
        }
        catch (OperationCanceledException)
        {
            // HttpClient.Timeout elapsed: we sent a request and got no answer in time.
            return ApiResult<T>.Fail(ApiFailure.Unreachable, null,
                "The service did not respond in time." + WriteWarning(isWrite));
        }
        catch (HttpRequestException)
        {
            // Connection refused, DNS failure, connection dropped: NO HTTP response was received.
            return ApiResult<T>.Fail(ApiFailure.Unreachable, null,
                $"Cannot reach the Equipment Borrowing service at {_http.BaseAddress}. " +
                "Check that the API is running, then try again." + WriteWarning(isWrite));
        }
        catch (JsonException)
        {
            return ApiResult<T>.Fail(ApiFailure.Unexpected, null, "The service returned a response this application could not read.");
        }
    }

    // A write whose answer never arrived MAY still have been committed (same idea as Lab 4's unknown outcome).
    private static string WriteWarning(bool isWrite) => isWrite
        ? " The request may or may not have been processed. Refresh and check before trying again."
        : "";

    private static async Task<ApiResult<T>> ReadFailureAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        ApiErrorBody? error = null;
        try { error = await response.Content.ReadFromJsonAsync<ApiErrorBody>(Json, ct); }
        catch (JsonException) { /* body was not our {code,message}; fall back below */ }

        var code = error?.Code;
        var status = (int)response.StatusCode;
        var apiMessage = string.IsNullOrWhiteSpace(error?.Message) ? $"The service returned HTTP {status}." : error!.Message;

        if (response.StatusCode == HttpStatusCode.BadRequest) return ApiResult<T>.Fail(ApiFailure.Validation, code, apiMessage);
        if (response.StatusCode == HttpStatusCode.NotFound) return ApiResult<T>.Fail(ApiFailure.NotFound, code, apiMessage);

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            var kind = code switch
            {
                "duplicate" => ApiFailure.Duplicate,
                "rule_violation" => ApiFailure.Rejected,
                _ => ApiFailure.Conflict
            };
            return ApiResult<T>.Fail(kind, code, apiMessage);
        }

        if (status >= 500)
            return ApiResult<T>.Fail(ApiFailure.Server, code, "The service is running but reported an error. " + apiMessage);

        return ApiResult<T>.Fail(ApiFailure.Unexpected, code, apiMessage);
    }
}
