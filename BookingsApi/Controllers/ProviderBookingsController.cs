using BookingsApi.Dtos;
using BookingsApi.Infrastructure;
using BookingsApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookingsApi.Controllers;

[ApiController]
[Authorize(Roles = "Provider")]
[Route("api/provider/bookings")]
public sealed class ProviderBookingsController(BookingService bookings) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<ProviderBookingDto>>> List(CancellationToken ct) =>
        await bookings.ListForProviderAsync(User.GetUserId(), ct);
}
