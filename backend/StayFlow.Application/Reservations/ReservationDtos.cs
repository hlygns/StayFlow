using System.ComponentModel.DataAnnotations;
using StayFlow.Domain.Enums;

namespace StayFlow.Application.Reservations;

public class GuestInput
{
    [Required, MaxLength(200)] public string FullName { get; set; } = string.Empty;
    [EmailAddress] public string? Email { get; set; }
    [MaxLength(30)] public string? Phone { get; set; }
    [MaxLength(20)] public string? IdentityNumber { get; set; }
}

public class CreateReservationRequest
{
    [Required] public Guid RoomId { get; set; }
    public DateOnly CheckIn { get; set; }
    public DateOnly CheckOut { get; set; }
    [Range(1, 20)] public int GuestCount { get; set; } = 1;
    [Required] public GuestInput Guest { get; set; } = new();
}

public record GuestDto(Guid Id, string FullName, string? Email, string? Phone);

public record ReservationDto(
    Guid Id,
    Guid RoomId,
    string RoomNumber,
    string RoomTypeName,
    DateOnly CheckIn,
    DateOnly CheckOut,
    int Nights,
    int GuestCount,
    ReservationStatus Status,
    decimal TotalPrice,
    DateTime? PaymentDeadline,
    DateTime CreatedAt,
    GuestDto Guest);
