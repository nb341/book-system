namespace BookingsApi.Domain;

public sealed class Slot
{
    public Guid Id { get; set; }
    public Guid ResourceId { get; set; }
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    public int PriceCents { get; set; }

    public Resource? Resource { get; set; }
}
