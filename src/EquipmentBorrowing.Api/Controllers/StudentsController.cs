using EquipmentBorrowing.Api.Contracts;
using EquipmentBorrowing.Api.Support;
using EquipmentBorrowing.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace EquipmentBorrowing.Api.Controllers;

[ApiController]
[Route("api/v1/students")]
[ProducesResponseType<ApiError>(StatusCodes.Status500InternalServerError)]
public class StudentsController : ControllerBase
{
    private const int MaxSearchTextLength = 100;

    private readonly FindStudentsService _find;
    private readonly GetStudentService _get;
    private readonly ViewStudentHistoryService _history;
    private readonly RegisterStudentService _register;
    private readonly UpdateStudentService _update;
    private readonly ChangeStudentEligibilityService _eligibility;

    public StudentsController(
        FindStudentsService find,
        GetStudentService get,
        ViewStudentHistoryService history,
        RegisterStudentService register,
        UpdateStudentService update,
        ChangeStudentEligibilityService eligibility)
    {
        _find = find;
        _get = get;
        _history = history;
        _register = register;
        _update = update;
        _eligibility = eligibility;
    }

    [HttpGet]
    [EndpointSummary("API-02: List students, optionally filtered by name or student number")]
    [ProducesResponseType<IReadOnlyList<StudentDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<StudentDto>>> List(
        [FromQuery] string? search, CancellationToken cancellationToken)
    {
        if (search is { Length: > MaxSearchTextLength })
            return ApiResults.Error(
                StatusCodes.Status400BadRequest,
                ApiErrorCodes.ValidationFailed,
                $"Search text can be at most {MaxSearchTextLength} characters.");

        var students = await _find.ExecuteAsync(search, cancellationToken);
        return Ok(students.Select(s => s.ToDto()).ToList());
    }

    [HttpGet("{id}", Name = "GetStudentById")]
    [EndpointSummary("API-03: Get one student")]
    [ProducesResponseType<StudentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StudentDto>> Get(int id, CancellationToken cancellationToken)
    {
        var result = await _get.ExecuteAsync(id, cancellationToken);
        if (!result.IsSuccess)
            return ApiResults.Failure(result);

        return result.Value!.ToDto();
    }

    // Writes use CancellationToken.None: a client that disconnects must not abort a commit halfway.
    [HttpPost]
    [EndpointSummary("API-04: Register a student (new students start eligible)")]
    [ProducesResponseType<StudentDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<StudentDto>> Register([FromBody] StudentRequest request)
    {
        var result = await _register.ExecuteAsync(
            request.StudentNumber, request.FullName, request.Contact, CancellationToken.None);
        if (!result.IsSuccess)
            return ApiResults.Failure(result);

        var dto = result.Value!.ToDto();

        // 201 + Location header pointing at API-03 (that is why GetStudentById has a route name).
        return CreatedAtRoute("GetStudentById", new { id = dto.Id }, dto);
    }

    [HttpPut("{id}")]
    [EndpointSummary("API-05: Replace a student's editable fields (contact: null clears it)")]
    [ProducesResponseType<StudentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiError>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<StudentDto>> Update(int id, [FromBody] StudentRequest request)
    {
        var result = await _update.ExecuteAsync(
            id, request.StudentNumber, request.FullName, request.Contact, CancellationToken.None);
        if (!result.IsSuccess)
            return ApiResults.Failure(result);

        return result.Value!.ToDto();
    }

    [HttpPatch("{id}/eligibility")]
    [EndpointSummary("API-06: Permit or restrict borrowing; the same value again is a no-op")]
    [ProducesResponseType<StudentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StudentDto>> ChangeEligibility(int id, [FromBody] EligibilityRequest request)
    {
        var result = await _eligibility.ExecuteAsync(id, request.IsEligible, CancellationToken.None);
        if (!result.IsSuccess)
            return ApiResults.Failure(result);

        return result.Value!.ToDto();
    }

    [HttpGet("{id}/borrowings")]
    [EndpointSummary("API-15: Borrowing history of one student (active and returned)")]
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
