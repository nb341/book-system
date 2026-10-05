namespace BookingsApi.Domain;

public sealed class Resource
{
    public Guid Id { get; set; }
    public Guid ProviderId { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }

    public User? Provider { get; set; }
    public List<Slot> Slots { get; set; } = [];
}
