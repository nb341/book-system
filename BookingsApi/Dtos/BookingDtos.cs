using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using BookingsApi.Domain;

namespace BookingsApi.Dtos;

public sealed record CreateBookingRequest
{
    [Required] public Guid? SlotId { get; init; }

    [Required, MaxLength(200)] public string? CardToken { get; init; }
}

public sealed record BookingDto(
    Guid Id, Guid SlotId, string ResourceName, DateTime StartUtc, DateTime EndUtc, [property: JsonConverter(typeof(JsonStringEnumConverter))] BookingStatus Status, int AmountCents);
