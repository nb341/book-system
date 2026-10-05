using BookingsApi.Data;
using BookingsApi.Domain;
using BookingsApi.Dtos;
using BookingsApi.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BookingsApi.Services;

public sealed class ResourceService(AppDbContext db)
{
    private static readonly BookingStatus[] Active = [BookingStatus.Pending, BookingStatus.Confirmed];

    public async Task<ResourceDto> CreateResourceAsync(Guid providerId, CreateResourceRequest request, CancellationToken ct)
    {
        var name = request.Name!.Trim();
        if (name.Length == 0)
            throw AppException.Validation("name", "Name is required.");
        var description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();

        var resource = new Resource
        {
            Id = Guid.NewGuid(),
            ProviderId = providerId,
            Name = name,
            Description = description,
            CreatedAt = DateTime.UtcNow
        };
        db.Resources.Add(resource);
        await db.SaveChangesAsync(ct);
        return new ResourceDto(resource.Id, resource.Name, resource.Description);
    }

    public async Task<ResourceDto> UpdateResourceAsync(
        Guid providerId, Guid resourceId, UpdateResourceRequest request, CancellationToken ct)
    {
        var name = request.Name!.Trim();
        if (name.Length == 0)
            throw AppException.Validation("name", "Name is required.");

        var resource = await db.Resources
            .FirstOrDefaultAsync(r => r.Id == resourceId && r.ProviderId == providerId, ct)
            ?? throw new AppException(StatusCodes.Status404NotFound, "Resource not found");
        resource.Name = name;
        resource.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        await db.SaveChangesAsync(ct);
        return new ResourceDto(resource.Id, resource.Name, resource.Description);
    }

    /// <summary>Deletes the resource and its slots, unless any slot has booking history (same rule as slot deletion).</summary>
    public async Task DeleteResourceAsync(Guid providerId, Guid resourceId, CancellationToken ct)
    {
        var resource = await db.Resources
            .FirstOrDefaultAsync(r => r.Id == resourceId && r.ProviderId == providerId, ct)
            ?? throw new AppException(StatusCodes.Status404NotFound, "Resource not found");

        if (await db.Bookings.AnyAsync(b => b.Slot!.ResourceId == resourceId, ct))
            throw ResourceHasBookings();

        // One SaveChanges = one transaction; a booking that lands after the check trips the FK instead.
        db.Slots.RemoveRange(await db.Slots.Where(s => s.ResourceId == resourceId).ToListAsync(ct));
        db.Resources.Remove(resource);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation })
        {
            throw ResourceHasBookings();
        }
    }

    public Task<List<ResourceDto>> ListOwnResourcesAsync(Guid providerId, CancellationToken ct) =>
        db.Resources.AsNoTracking()
            .Where(r => r.ProviderId == providerId)
            .OrderBy(r => r.Name).ThenBy(r => r.CreatedAt)
            .Select(r => new ResourceDto(r.Id, r.Name, r.Description))
            .ToListAsync(ct);

    public async Task<List<ProviderSlotDto>> ListOwnSlotsAsync(Guid providerId, Guid resourceId, CancellationToken ct)
    {
        await EnsureOwnedAsync(providerId, resourceId, ct);
        var since = DateTime.UtcNow.AddDays(-30);
        return await db.Slots.AsNoTracking()
            .Where(s => s.ResourceId == resourceId && s.EndUtc >= since)
            .OrderBy(s => s.StartUtc)
            .Select(s => new ProviderSlotDto(s.Id, s.StartUtc, s.EndUtc, s.PriceCents,
                db.Bookings.Any(b => b.SlotId == s.Id && Active.Contains(b.Status))))
            .ToListAsync(ct);
    }

    public async Task<ProviderSlotDto> CreateSlotAsync(
        Guid providerId, Guid resourceId, CreateSlotRequest request, CancellationToken ct)
    {
        await EnsureOwnedAsync(providerId, resourceId, ct);

        var start = request.StartUtc!.Value.UtcDateTime;
        var end = request.EndUtc!.Value.UtcDateTime;
        var errors = new Dictionary<string, string[]>();
        if (end <= start) errors["endUtc"] = ["endUtc must be after startUtc."];
        if (start <= DateTime.UtcNow) errors["startUtc"] = ["startUtc must be in the future."];
        if (request.PriceCents!.Value < 0) errors["priceCents"] = ["priceCents must be zero or greater."];
        if (errors.Count > 0)
            throw new AppException(StatusCodes.Status400BadRequest, "Validation failed",
                "One or more validation errors occurred.", errors);

        var slot = new Slot
        {
            Id = Guid.NewGuid(),
            ResourceId = resourceId,
            StartUtc = start,
            EndUtc = end,
            PriceCents = request.PriceCents.Value
        };
        db.Slots.Add(slot);
        await db.SaveChangesAsync(ct); // overlap -> 23P01 -> 409 in GlobalExceptionHandler
        return new ProviderSlotDto(slot.Id, slot.StartUtc, slot.EndUtc, slot.PriceCents, false);
    }

    public async Task DeleteSlotAsync(Guid providerId, Guid slotId, CancellationToken ct)
    {
        var slot = await db.Slots
            .FirstOrDefaultAsync(s => s.Id == slotId && s.Resource!.ProviderId == providerId, ct)
            ?? throw new AppException(StatusCodes.Status404NotFound, "Slot not found");

        if (await db.Bookings.AnyAsync(b => b.SlotId == slotId, ct))
            throw SlotHasBookings();

        db.Slots.Remove(slot);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation })
        {
            throw SlotHasBookings();
        }
    }

    public Task<List<PublicResourceDto>> ListAllResourcesAsync(CancellationToken ct) =>
        db.Resources.AsNoTracking()
            .OrderBy(r => r.Name).ThenBy(r => r.CreatedAt)
            .Select(r => new PublicResourceDto(r.Id, r.Name, r.Description, r.Provider!.Name))
            .ToListAsync(ct);

    public async Task<List<SlotDto>> ListAvailableSlotsAsync(
        Guid resourceId, DateTimeOffset? from, DateTimeOffset? to, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var fromUtc = from?.UtcDateTime is { } f && f > now ? f : now;
        var toUtc = to?.UtcDateTime;
        if (from is not null && toUtc is not null && from.Value.UtcDateTime > toUtc)
            throw AppException.Validation("from", "from must not be after to.");

        if (!await db.Resources.AnyAsync(r => r.Id == resourceId, ct))
            throw new AppException(StatusCodes.Status404NotFound, "Resource not found");

        var query = db.Slots.AsNoTracking()
            .Where(s => s.ResourceId == resourceId && s.StartUtc >= fromUtc
                && !db.Bookings.Any(b => b.SlotId == s.Id && Active.Contains(b.Status)));
        if (toUtc is { } t) query = query.Where(s => s.StartUtc <= t);

        return await query.OrderBy(s => s.StartUtc)
            .Select(s => new SlotDto(s.Id, s.StartUtc, s.EndUtc, s.PriceCents))
            .ToListAsync(ct);
    }

    private async Task EnsureOwnedAsync(Guid providerId, Guid resourceId, CancellationToken ct)
    {
        if (!await db.Resources.AnyAsync(r => r.Id == resourceId && r.ProviderId == providerId, ct))
            throw new AppException(StatusCodes.Status404NotFound, "Resource not found");
    }

    private static AppException SlotHasBookings() =>
        new(StatusCodes.Status409Conflict, "Conflict", "Slot has bookings and cannot be deleted.");

    private static AppException ResourceHasBookings() =>
        new(StatusCodes.Status409Conflict, "Conflict", "Resource has bookings and cannot be deleted.");
}
