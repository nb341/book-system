using System.ComponentModel.DataAnnotations;

namespace BookingsApi.Dtos;

/// <summary>Body for both creating and updating (full replace) a resource.</summary>
public sealed record ResourceRequest
{
    [Required, MaxLength(200)] public string? Name { get; init; }

    [MaxLength(2000)] public string? Description { get; init; }
}

public sealed record CreateSlotRequest
{
    [Required] public DateTimeOffset? StartUtc { get; init; }

    [Required] public DateTimeOffset? EndUtc { get; init; }

    [Required] public int? PriceCents { get; init; }
}

public sealed record ResourceDto(Guid Id, string Name, string? Description);

public sealed record PublicResourceDto(Guid Id, string Name, string? Description, string ProviderName);

public sealed record SlotDto(Guid Id, DateTime StartUtc, DateTime EndUtc, int PriceCents);

public sealed record ProviderSlotDto(Guid Id, DateTime StartUtc, DateTime EndUtc, int PriceCents, bool IsBooked);
