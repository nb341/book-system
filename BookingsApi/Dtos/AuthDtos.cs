using System.ComponentModel.DataAnnotations;

namespace BookingsApi.Dtos;

public sealed record RegisterRequest
{
    [Required, MaxLength(256)] public string? Email { get; init; }

    [Required, MinLength(8, ErrorMessage = "Password must be at least 8 characters."), MaxLength(128)]
    public string? Password { get; init; }

    [Required, MaxLength(200)] public string? Name { get; init; }

    [Required] public string? Role { get; init; }
}

public sealed record LoginRequest
{
    [Required] public string? Email { get; init; }

    [Required] public string? Password { get; init; }
}

public sealed record UserDto(Guid Id, string Email, string Name, string Role);

public sealed record AuthResponse(string Token, UserDto User);
