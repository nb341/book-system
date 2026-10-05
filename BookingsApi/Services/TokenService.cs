using System.Text;
using BookingsApi.Domain;
using BookingsApi.Infrastructure;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace BookingsApi.Services;

public sealed class TokenService(IOptions<JwtOptions> options)
{
    private readonly JwtOptions _jwt = options.Value;

    public string Create(User user)
    {
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _jwt.Issuer,
            Audience = _jwt.Audience,
            Expires = DateTime.UtcNow.AddMinutes(_jwt.ExpiryMinutes),
            Claims = new Dictionary<string, object>
            {
                ["sub"] = user.Id.ToString(),
                ["email"] = user.Email,
                ["name"] = user.Name,
                ["role"] = user.Role.ToString()
            },
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Key)), SecurityAlgorithms.HmacSha256)
        };
        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}
