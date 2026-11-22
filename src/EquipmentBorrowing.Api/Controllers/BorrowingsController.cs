using EquipmentBorrowing.Api.Contracts;
using EquipmentBorrowing.Api.Support;
using EquipmentBorrowing.Application.Dtos;
using EquipmentBorrowing.Application.Services;
using EquipmentBorrowing.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace EquipmentBorrowing.Api.Controllers;

[ApiController]
[Route("api/v1/borrowings")]
[ProducesResponseType<ApiError>(StatusCodes.Status500InternalServerError)]
public class BorrowingsController : ControllerBase
{
    private readonly GetBorrowingService _get;
    private readonly SearchBorrowingsService _search;
    private readonly BorrowEquipmentService _borrow;
    private readonly ReturnEquipmentService _return;

    public BorrowingsController(
        GetBorrowingService get,
        SearchBorrowingsService search,
        BorrowEquipmentService borrow,
        ReturnEquipmentService returnEquipment)
    {
        _get = get;
        _search = search;
        _borrow = borrow;
        _return = returnEquipment;
    }

    [HttpPost]
    [EndpointSummary("API-10: Issue one item to a student")]
    [ProducesResponseType<BorrowingDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiError>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BorrowingDto>> Borrow([FromBody] BorrowRequest request)
    {
        // CancellationToken.None on purpose: once the transaction starts, a client that hangs up
        // must not be able to cancel it halfway and leave the outcome in doubt (BR-18).
        var result = await _borrow.ExecuteAsync(
            request.StudentId, request.EquipmentId, request.ExpectedReturnDate, CancellationToken.None);
        if (!result.IsSuccess)
            return ApiResults.Failure(result);

        var saved = await LoadAsync(result.Value!.Id);
        if (!saved.IsSuccess)
            return ApiResults.Failure(saved);

        var dto = saved.Value!.ToDto();
        return CreatedAtRoute("GetBorrowingById", new { id = dto.Id }, dto);
    }

    [HttpPost("{id}/return")]
    [EndpointSummary("API-12: Return a borrowed item (the server sets the return date)")]
    [ProducesResponseType<BorrowingDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiError>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BorrowingDto>> Return(
        int id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] ReturnRequest? request)
    {
        // "request" is never read: the body is {} or absent. Its only job is to make
        // ASP.NET reject any extra field.
        var result = await _return.ExecuteAsync(id, CancellationToken.None);
        if (!result.IsSuccess)
            return ApiResults.Failure(result);

        var saved = await LoadAsync(id);
        if (!saved.IsSuccess)
            return ApiResults.Failure(saved);

        return saved.Value!.ToDto();
    }

    [HttpGet("{id}", Name = "GetBorrowingById")]
    [EndpointSummary("API-13: Get one borrowing")]
    [ProducesResponseType<BorrowingDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BorrowingDto>> Get(int id, CancellationToken cancellationToken)
    {
        var result = await _get.ExecuteAsync(id, cancellationToken);
        if (!result.IsSuccess)
            return ApiResults.Failure(result);

        return result.Value!.ToDto();
    }

    [HttpGet]
    [EndpointSummary("API-14: Search, filter and sort borrowings")]
    [ProducesResponseType<IReadOnlyList<BorrowingDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<BorrowingDto>>> Search(
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] string? sort,
        [FromQuery] string? direction,
        CancellationToken cancellationToken)
    {
        if (!TryParseStatus(status, out var parsedStatus))
            return Invalid("status must be Active, Returned or All.");

        if (!TryParseSort(sort, out var parsedSort))
            return Invalid("sort must be borrowedDate or expectedReturnDate.");

        if (!TryParseDirection(direction, out var descending))
            return Invalid("direction must be asc or desc.");

        var query = new BorrowingQuery(search, parsedStatus, parsedSort, descending);
        var result = await _search.ExecuteAsync(query, cancellationToken);
        if (!result.IsSuccess)
            return ApiResults.Failure(result);

        return Ok(result.Value!.Select(r => r.ToDto()).ToList());
    }

    // The write already committed, so this read must not be cut short by a client hang-up.
    private Task<OperationResult<BorrowingRecord>> LoadAsync(int borrowingId) =>
        _get.ExecuteAsync(borrowingId, CancellationToken.None);

    private static ObjectResult Invalid(string message) =>
        ApiResults.Error(StatusCodes.Status400BadRequest, ApiErrorCodes.ValidationFailed, message);

    // Parsed by hand on purpose: Enum.TryParse would also accept "1" or "7" as a valid status.
    // Missing = the contract's default; anything else must match exactly (case-insensitive).
    private static bool TryParseStatus(string? text, out BorrowingStatus? status)
    {
        switch (text?.ToLowerInvariant())
        {
            case null:
            case "all":
                status = null;
                return true;
            case "active":
                status = BorrowingStatus.Active;
                return true;
            case "returned":
                status = BorrowingStatus.Returned;
                return true;
            default:
                status = null;
                return false;
        }
    }

    private static bool TryParseSort(string? text, out BorrowingSortField sort)
    {
        switch (text?.ToLowerInvariant())
        {
            case null:
            case "borroweddate":
                sort = BorrowingSortField.DateBorrowed;
                return true;
            case "expectedreturndate":
                sort = BorrowingSortField.ExpectedReturnDate;
                return true;
            default:
                sort = BorrowingSortField.DateBorrowed;
                return false;
        }
    }

    private static bool TryParseDirection(string? text, out bool descending)
    {
        switch (text?.ToLowerInvariant())
        {
            case null:
            case "asc":
                descending = false;   // contract default: ascending
                return true;
            case "desc":
                descending = true;
                return true;
            default:
                descending = false;
                return false;
        }
    }
}
