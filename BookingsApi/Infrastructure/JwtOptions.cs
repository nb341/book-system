namespace BookingsApi.Infrastructure;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Key { get; init; } = "";
    public string Issuer { get; init; } = "BookingsApi";
    public string Audience { get; init; } = "BookingsApp";
    public int ExpiryMinutes { get; init; } = 60;
}
