using EquipmentBorrowing.Api.Contracts;
using EquipmentBorrowing.Api.Support;
using EquipmentBorrowing.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace EquipmentBorrowing.Api.Controllers;

[ApiController]
[Route("api/v1/checkout-requests")]
[ProducesResponseType<ApiError>(StatusCodes.Status500InternalServerError)]
public class CheckoutRequestsController : ControllerBase
{
    private readonly CheckOutMultipleEquipmentService _checkout;
    private readonly GetBorrowingService _get;

    public CheckoutRequestsController(CheckOutMultipleEquipmentService checkout, GetBorrowingService get)
    {
        _checkout = checkout;
        _get = get;
    }

    [HttpPost]
    [EndpointSummary("API-11: Issue two or more items to one student, all or nothing")]
    [ProducesResponseType<CheckoutResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiError>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CheckoutResponse>> Checkout([FromBody] CheckoutRequest request)
    {
        // CancellationToken.None: a started transaction must finish (see BorrowingsController.Borrow).
        var result = await _checkout.ExecuteAsync(
            request.StudentId, request.EquipmentIds, request.ExpectedReturnDate, CancellationToken.None);
        if (!result.IsSuccess)
            return ApiResults.Failure(result);

        // All items were committed together; now read each one back with its names.
        var borrowings = new List<BorrowingDto>();
        foreach (var created in result.Value!)
        {
            var saved = await _get.ExecuteAsync(created.Id, CancellationToken.None);
            if (!saved.IsSuccess)
                return ApiResults.Failure(saved);

            borrowings.Add(saved.Value!.ToDto());
        }

        // The contract gives API-11 a body but no Location header.
        return StatusCode(StatusCodes.Status201Created, new CheckoutResponse(borrowings));
    }
}
