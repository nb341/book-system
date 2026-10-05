using BookingsApi.Dtos;
using BookingsApi.Infrastructure;
using BookingsApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookingsApi.Controllers;

[ApiController]
[Authorize(Roles = "Customer")]
[Route("api/bookings")]
public sealed class BookingsController(BookingService bookings) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<BookingDto>> Create(
        CreateBookingRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw AppException.Validation("Idempotency-Key", "The Idempotency-Key header is required.");
        if (idempotencyKey.Length > 64)
            throw AppException.Validation("Idempotency-Key", "The Idempotency-Key must be at most 64 characters.");

        var booking = await bookings.CreateAsync(User.GetUserId(), idempotencyKey, request, ct);
        return StatusCode(StatusCodes.Status201Created, booking);
    }

    [HttpGet]
    public async Task<ActionResult<List<BookingDto>>> List(CancellationToken ct) =>
        await bookings.ListOwnAsync(User.GetUserId(), ct);

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<BookingDto>> Cancel(Guid id, CancellationToken ct) =>
        await bookings.CancelAsync(User.GetUserId(), id, ct);

    [HttpPost("{id:guid}/reschedule")]
    public async Task<ActionResult<BookingDto>> Reschedule(Guid id, RescheduleBookingRequest request, CancellationToken ct) =>
        await bookings.RescheduleAsync(User.GetUserId(), id, request, ct);
}
