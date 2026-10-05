using System.Security.Claims;

namespace BookingsApi.Infrastructure;

public static class ClaimsPrincipalExtensions
{
    /// <summary>Caller id from the token's sub claim. Always use this for ownership, never a body id.</summary>
    public static Guid GetUserId(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue("sub"), out var id)
            ? id
            : throw new AppException(StatusCodes.Status401Unauthorized, "Unauthorized");
}
