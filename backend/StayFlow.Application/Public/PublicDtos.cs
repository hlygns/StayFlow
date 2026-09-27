using System.ComponentModel.DataAnnotations;
using StayFlow.Domain.Enums;

namespace StayFlow.Application.Public;

// Misafir uygulamasinin gordugu veriler. Personel DTO'larindan ayri: oda numarasi, oda Id'si,
// diger misafirler, ic notlar gibi bilgiler burada YOK.

public record PublicHotelSummary(
    string Slug,
    string Name,
    string City,
    string Description,
    decimal? MinPricePerNight,
    double? AverageRating,
    int ReviewCount);

public record PublicRoomType(Guid Id, string Name, string Description, int Capacity, decimal BasePrice);

public record PublicHotelDetail(
    string Slug,
    string Name,
    string City,
    string Address,
    string Phone,
    string Description,
    double? AverageRating,
    int ReviewCount,
    List<PublicRoomType> RoomTypes);

public record PublicAvailability(
    Guid RoomTypeId,
    string Name,
    string Description,
    int Capacity,
    int Nights,
    decimal TotalPrice,
    /// <summary>"Son 2 oda" gibi bir bilgi icin; kesin oda numaralari gizli.</summary>
    int RoomsLeft);

public class PublicGuestInput
{
    [Required, MaxLength(200)] public string FullName { get; set; } = string.Empty;
    /// <summary>Misafir icin zorunlu: rezervasyonuna kod + e-posta ile erisir, onay e-postasi buraya gider.</summary>
    [Required, EmailAddress] public string Email { get; set; } = string.Empty;
    [MaxLength(30)] public string? Phone { get; set; }
}

public class PublicBookingRequest
{
    [Required] public Guid RoomTypeId { get; set; }
    public DateOnly CheckIn { get; set; }
    public DateOnly CheckOut { get; set; }
    [Range(1, 20)] public int GuestCount { get; set; } = 1;
    [Required] public PublicGuestInput Guest { get; set; } = new();
}

public class GuestAccessRequest
{
    [Required, EmailAddress] public string Email { get; set; } = string.Empty;
}

/// <summary>
/// SIMULE odeme: gercek bir odeme saglayicisi (iyzico, Stripe) entegre edilene kadar.
/// Kart numarasinin tamami ASLA saklanmaz; sadece son 4 hane.
/// Test kartlari: 4242 4242 4242 4242 basarili, 4000 0000 0000 0002 reddedilir.
/// </summary>
public class SimulatedPaymentRequest
{
    [Required, EmailAddress] public string Email { get; set; } = string.Empty;
    [Required, MaxLength(100)] public string CardHolder { get; set; } = string.Empty;
    [Required] public string CardNumber { get; set; } = string.Empty;
    [Required, RegularExpression(@"^(0[1-9]|1[0-2])/\d{2}$", ErrorMessage = "Son kullanma tarihi AA/YY biçiminde olmalı.")]
    public string Expiry { get; set; } = string.Empty;
    [Required, RegularExpression(@"^\d{3,4}$", ErrorMessage = "CVC 3 veya 4 haneli olmalı.")]
    public string Cvc { get; set; } = string.Empty;
}

public record GuestReservation(
    string ConfirmationCode,
    ReservationStatus Status,
    string HotelSlug,
    string HotelName,
    string HotelAddress,
    string HotelPhone,
    string RoomTypeName,
    DateOnly CheckIn,
    DateOnly CheckOut,
    int Nights,
    int GuestCount,
    decimal TotalPrice,
    DateTime? PaymentDeadline,
    string GuestName,
    bool CanPay,
    bool CanCancel,
    string? PaymentMethod,
    bool CanReview,
    int? MyRating);

public class GuestReviewRequest
{
    [Required, EmailAddress] public string Email { get; set; } = string.Empty;
    [Range(1, 5)] public int Rating { get; set; }
    [Required, MinLength(10, ErrorMessage = "Yorum en az 10 karakter olmalı."), MaxLength(2000)]
    public string Comment { get; set; } = string.Empty;
}

public class PushTokenRequest
{
    [Required, EmailAddress] public string Email { get; set; } = string.Empty;
    /// <summary>Expo push token: "ExponentPushToken[xxxx]"</summary>
    [Required, MaxLength(200), RegularExpression(@"^Expo(nent)?PushToken\[[A-Za-z0-9_\-]+\]$", ErrorMessage = "Geçersiz bildirim anahtarı.")]
    public string Token { get; set; } = string.Empty;
}

public record PublicReview(int Rating, string Comment, string GuestName, DateTime CreatedAt, string? OwnerReply);
