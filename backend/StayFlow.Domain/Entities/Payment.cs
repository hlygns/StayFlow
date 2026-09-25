using StayFlow.Domain.Common;
using StayFlow.Domain.Enums;

namespace StayFlow.Domain.Entities;

public class Payment : ITenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid HotelId { get; set; }
    public Hotel Hotel { get; set; } = null!;

    public Guid ReservationId { get; set; }
    public Reservation Reservation { get; set; } = null!;

    public decimal Amount { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public string? Method { get; set; }
    public DateTime? PaidAt { get; set; }
}
