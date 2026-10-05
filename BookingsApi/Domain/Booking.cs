namespace BookingsApi.Domain;

public sealed class Booking
{
    public Guid Id { get; set; }
    public Guid SlotId { get; set; }
    public Guid CustomerId { get; set; }
    public BookingStatus Status { get; set; }
    public int AmountCents { get; set; }
    public string? PaymentRef { get; set; }
    public required string IdempotencyKey { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Slot? Slot { get; set; }
    public User? Customer { get; set; }
}
