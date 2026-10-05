namespace BookingsApi.Domain;

public sealed class User
{
    public Guid Id { get; set; }
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
    public required string Name { get; set; }
    public Role Role { get; set; }
    public DateTime CreatedAt { get; set; }
}
