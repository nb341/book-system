using BookingsApi.Dtos;
using BookingsApi.Infrastructure;
using BookingsApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookingsApi.Controllers;

[ApiController]
public sealed class ResourcesController(ResourceService resources) : ControllerBase
{
    [HttpGet("api/resources")]
    public async Task<ActionResult<List<PublicResourceDto>>> ListAll(CancellationToken ct) =>
        await resources.ListAllResourcesAsync(ct);

    [HttpGet("api/resources/{id:guid}/slots")]
    public async Task<ActionResult<List<SlotDto>>> AvailableSlots(
        Guid id, [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to, CancellationToken ct) =>
        await resources.ListAvailableSlotsAsync(id, from, to, ct);

    [Authorize(Roles = "Provider")]
    [HttpPost("api/provider/resources")]
    public async Task<ActionResult<ResourceDto>> Create(CreateResourceRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created,
            await resources.CreateResourceAsync(User.GetUserId(), request, ct));

    [Authorize(Roles = "Provider")]
    [HttpGet("api/provider/resources")]
    public async Task<ActionResult<List<ResourceDto>>> ListOwn(CancellationToken ct) =>
        await resources.ListOwnResourcesAsync(User.GetUserId(), ct);

    [Authorize(Roles = "Provider")]
    [HttpPut("api/provider/resources/{id:guid}")]
    public async Task<ActionResult<ResourceDto>> Update(Guid id, UpdateResourceRequest request, CancellationToken ct) =>
        await resources.UpdateResourceAsync(User.GetUserId(), id, request, ct);

    [Authorize(Roles = "Provider")]
    [HttpDelete("api/provider/resources/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await resources.DeleteResourceAsync(User.GetUserId(), id, ct);
        return NoContent();
    }

    [Authorize(Roles = "Provider")]
    [HttpGet("api/provider/resources/{id:guid}/slots")]
    public async Task<ActionResult<List<ProviderSlotDto>>> ListOwnSlots(Guid id, CancellationToken ct) =>
        await resources.ListOwnSlotsAsync(User.GetUserId(), id, ct);

    [Authorize(Roles = "Provider")]
    [HttpPost("api/provider/resources/{id:guid}/slots")]
    public async Task<ActionResult<ProviderSlotDto>> CreateSlot(Guid id, CreateSlotRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created,
            await resources.CreateSlotAsync(User.GetUserId(), id, request, ct));

    [Authorize(Roles = "Provider")]
    [HttpDelete("api/provider/slots/{id:guid}")]
    public async Task<IActionResult> DeleteSlot(Guid id, CancellationToken ct)
    {
        await resources.DeleteSlotAsync(User.GetUserId(), id, ct);
        return NoContent();
    }
}
