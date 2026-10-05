using BookingsApi.Data;
using BookingsApi.Domain;
using BookingsApi.Dtos;
using BookingsApi.Infrastructure;
using BookingsApi.Payments;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BookingsApi.Services;

public sealed class BookingService(AppDbContext db, IPaymentGateway payments, ILogger<BookingService> logger)
{
    private const string SlotIndex = "UX_Bookings_SlotId_Active";

    public async Task<BookingDto> CreateAsync(
        Guid customerId, string idempotencyKey, CreateBookingRequest request, CancellationToken ct)
    {
        var slotId = request.SlotId!.Value;
        var cardToken = request.CardToken!;

        // 2. Replay a previously stored outcome.
        var existing = await FindByKeyAsync(customerId, idempotencyKey, ct);
        if (existing is not null) return await ReplayAsync(existing, ct);

        // 3. Validate the slot.
        var now = DateTime.UtcNow;
        var slot = await db.Slots.AsNoTracking().FirstOrDefaultAsync(s => s.Id == slotId, ct)
            ?? throw new AppException(StatusCodes.Status404NotFound, "Not found", "Slot not found.");
        if (slot.StartUtc <= now)
            throw new AppException(StatusCodes.Status409Conflict, "Slot unavailable", "The slot has already started.");

        // 4. Claim the slot with a Pending booking; the partial unique index arbitrates.
        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            SlotId = slotId,
            CustomerId = customerId,
            Status = BookingStatus.Pending,
            AmountCents = slot.PriceCents,
            IdempotencyKey = idempotencyKey,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.Bookings.Add(booking);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg)
        {
            db.ChangeTracker.Clear();
            if (pg.ConstraintName == SlotIndex)
                throw new AppException(StatusCodes.Status409Conflict, "Slot already booked",
                    "The selected slot is no longer available.");
            // Idempotency index: a concurrent duplicate request; return the original outcome.
            var original = await FindByKeyAsync(customerId, idempotencyKey, ct) ?? throw new InvalidOperationException("Duplicate key reported but booking not found.", ex);
            return await ReplayAsync(original, ct);
        }
        db.ChangeTracker.Clear();

        // 5. Charge outside any transaction.
        PaymentResult result;
        try
        {
            result = await payments.ChargeAsync(booking.AmountCents, cardToken, booking.Id, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Payment gateway error for booking {BookingId}", booking.Id);
            result = new PaymentResult(false, null, "The payment could not be processed.");
        }

        // 6/7. Conditional state change (never overwrites a non-Pending booking).
        if (result.Success)
        {
            await db.Bookings.Where(b => b.Id == booking.Id && b.Status == BookingStatus.Pending)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(b => b.Status, BookingStatus.Confirmed)
                    .SetProperty(b => b.PaymentRef, result.PaymentRef)
                    .SetProperty(b => b.UpdatedAt, DateTime.UtcNow), CancellationToken.None);
            return await LoadDtoAsync(booking.Id, CancellationToken.None);
        }

        await db.Bookings.Where(b => b.Id == booking.Id && b.Status == BookingStatus.Pending)
            .ExecuteUpdateAsync(s => s
                .SetProperty(b => b.Status, BookingStatus.PaymentFailed)
                .SetProperty(b => b.UpdatedAt, DateTime.UtcNow), CancellationToken.None);
        throw PaymentFailed(result.FailureReason);
    }

    public Task<List<BookingDto>> ListOwnAsync(Guid customerId, CancellationToken ct) =>
        db.Bookings.AsNoTracking()
            .Where(b => b.CustomerId == customerId)
            .OrderByDescending(b => b.CreatedAt)
            .Select(b => new BookingDto(b.Id, b.SlotId, b.Slot!.Resource!.Name, b.Slot.StartUtc, b.Slot.EndUtc,
                b.Status, b.AmountCents))
            .ToListAsync(ct);

    private Task<Booking?> FindByKeyAsync(Guid customerId, string key, CancellationToken ct) =>
        db.Bookings.AsNoTracking()
            .FirstOrDefaultAsync(b => b.CustomerId == customerId && b.IdempotencyKey == key, ct);

    private async Task<BookingDto> ReplayAsync(Booking b, CancellationToken ct)
    {
        if (b.Status == BookingStatus.PaymentFailed) throw PaymentFailed("The card was declined.");
        return await LoadDtoAsync(b.Id, ct);
    }

    private static AppException PaymentFailed(string? reason) =>
        new(StatusCodes.Status402PaymentRequired, "Payment failed", reason ?? "The card was declined.");

    private Task<BookingDto> LoadDtoAsync(Guid id, CancellationToken ct) =>
        db.Bookings.AsNoTracking().Where(b => b.Id == id)
            .Select(b => new BookingDto(b.Id, b.SlotId, b.Slot!.Resource!.Name, b.Slot.StartUtc, b.Slot.EndUtc,
                b.Status, b.AmountCents))
            .SingleAsync(ct);
}
