using EquipmentBorrowing.Api.Contracts;
using EquipmentBorrowing.Api.Support;
using EquipmentBorrowing.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace EquipmentBorrowing.Api.Controllers;

[ApiController]
[Route("api/v1/equipment")]
[ProducesResponseType<ApiError>(StatusCodes.Status500InternalServerError)]
public class EquipmentController : ControllerBase
{
    private readonly FindEquipmentService _find;
    private readonly GetEquipmentService _get;
    private readonly ViewEquipmentHistoryService _history;
    private readonly RegisterEquipmentService _register;
    private readonly UpdateEquipmentService _update;

    public EquipmentController(
        FindEquipmentService find,
        GetEquipmentService get,
        ViewEquipmentHistoryService history,
        RegisterEquipmentService register,
        UpdateEquipmentService update)
    {
        _find = find;
        _get = get;
        _history = history;
        _register = register;
        _update = update;
    }

    [HttpGet]
    [EndpointSummary("API-01: List equipment; available=true returns only items that can be borrowed now")]
    [ProducesResponseType<IReadOnlyList<EquipmentDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<EquipmentDto>>> List(
        [FromQuery] bool? available, CancellationToken cancellationToken)
    {
        // A malformed value such as available=maybe never reaches this line:
        // [ApiController] rejects it with our 400 body. Absent or false = everything.
        var items = await _find.ExecuteAsync(
            searchText: null, availableOnly: available == true, cancellationToken);

        return Ok(items.Select(e => e.ToDto()).ToList());
    }

    [HttpGet("{id}", Name = "GetEquipmentById")]
    [EndpointSummary("API-07: Get one equipment item")]
    [ProducesResponseType<EquipmentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EquipmentDto>> Get(int id, CancellationToken cancellationToken)
    {
        var result = await _get.ExecuteAsync(id, cancellationToken);
        if (!result.IsSuccess)
            return ApiResults.Failure(result);

        return result.Value!.ToDto();
    }

    // Writes use CancellationToken.None: a client that disconnects must not abort a commit halfway.
    [HttpPost]
    [EndpointSummary("API-08: Register an equipment item (new items start available)")]
    [ProducesResponseType<EquipmentDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EquipmentDto>> Register([FromBody] EquipmentRequest request)
    {
        var result = await _register.ExecuteAsync(
            request.EquipmentCode, request.Name, request.Description, CancellationToken.None);
        if (!result.IsSuccess)
            return ApiResults.Failure(result);

        var dto = result.Value!.ToDto();
        return CreatedAtRoute("GetEquipmentById", new { id = dto.Id }, dto);
    }

    [HttpPut("{id}")]
    [EndpointSummary("API-09: Replace an item's descriptive fields (availability cannot be edited here)")]
    [ProducesResponseType<EquipmentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiError>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EquipmentDto>> Update(int id, [FromBody] EquipmentRequest request)
    {
        var result = await _update.ExecuteAsync(
            id, request.EquipmentCode, request.Name, request.Description, CancellationToken.None);
        if (!result.IsSuccess)
            return ApiResults.Failure(result);

        return result.Value!.ToDto();
    }

    [HttpGet("{id}/borrowings")]
    [EndpointSummary("API-16: Borrowing history of one equipment item (active and returned)")]
    [ProducesResponseType<IReadOnlyList<BorrowingDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<BorrowingDto>>> History(
        int id, CancellationToken cancellationToken)
    {
        var result = await _history.ExecuteAsync(id, cancellationToken);
        if (!result.IsSuccess)
            return ApiResults.Failure(result);

        return Ok(result.Value!.Records.Select(r => r.ToDto()).ToList());
    }
}
