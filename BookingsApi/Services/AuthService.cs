using System.Net.Mail;
using BookingsApi.Data;
using BookingsApi.Domain;
using BookingsApi.Dtos;
using BookingsApi.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BookingsApi.Services;

public sealed class AuthService(AppDbContext db, TokenService tokens)
{
    private static readonly PasswordHasher<User> Hasher = new();

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        var email = Normalize(request.Email);
        if (!MailAddress.TryCreate(email, out var parsed) || parsed.Address != email)
            throw AppException.Validation("email", "Email is not a valid address.");

        if (!Enum.TryParse<Role>(request.Role?.Trim(), ignoreCase: false, out var role) || !Enum.IsDefined(role))
            throw AppException.Validation("role", "Role must be Customer or Provider.");

        var name = request.Name!.Trim();
        if (name.Length == 0)
            throw AppException.Validation("name", "Name is required.");

        if (await db.Users.AnyAsync(u => u.Email == email, ct))
            throw EmailTaken();

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            Name = name,
            Role = role,
            PasswordHash = "",
            CreatedAt = DateTime.UtcNow
        };
        user.PasswordHash = Hasher.HashPassword(user, request.Password!);
        db.Users.Add(user);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw EmailTaken();
        }

        return ToResponse(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var email = Normalize(request.Email);
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == email, ct);

        if (user is null
            || Hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password!) == PasswordVerificationResult.Failed)
            throw new AppException(StatusCodes.Status401Unauthorized, "Invalid credentials",
                "Email or password is incorrect.");

        return ToResponse(user);
    }

    private AuthResponse ToResponse(User user) =>
        new(tokens.Create(user), new UserDto(user.Id, user.Email, user.Name, user.Role.ToString()));

    private static string Normalize(string? email) => (email ?? "").Trim().ToLowerInvariant();

    private static AppException EmailTaken() =>
        new(StatusCodes.Status409Conflict, "Email already registered",
            "An account with this email already exists.");
}
