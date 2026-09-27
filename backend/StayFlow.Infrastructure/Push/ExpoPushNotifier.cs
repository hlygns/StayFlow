using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StayFlow.Application.Common.Interfaces;

namespace StayFlow.Infrastructure.Push;

/// <summary>
/// Misafirin telefonuna push bildirim: Expo Push Service (https://exp.host/--/api/v2/push/send).
/// Expo, iOS (APNs) ve Android (FCM) farkini bizim yerimize yonetir; tek bir HTTP istegi yeterli.
/// Bildirim gonderilemezse rezervasyon islemi etkilenmez (hata loglanir, yutulur).
/// </summary>
public class ExpoPushNotifier : IReservationNotifier
{
    public const string HttpClientName = "expo-push";

    private readonly IApplicationDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ExpoPushNotifier> _logger;

    public ExpoPushNotifier(IApplicationDbContext db, IHttpClientFactory httpClientFactory, ILogger<ExpoPushNotifier> logger)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>Misafirin bilmesi gereken degisiklikler. "Created"/"CheckedOut" icin bildirim yok (misafir zaten ekranda).</summary>
    private static (string Title, string Body)? MessageFor(ReservationChange change, string hotelName) => change switch
    {
        ReservationChange.Confirmed => ("Rezervasyonunuz onaylandı", $"{hotelName} sizi bekliyor."),
        ReservationChange.CheckedIn => ($"{hotelName}'e hoş geldiniz", "İyi tatiller dileriz!"),
        ReservationChange.Cancelled => ("Rezervasyonunuz iptal edildi", $"{hotelName} rezervasyonunuz iptal edildi."),
        ReservationChange.AutoCancelled => ("Ödeme süresi doldu", $"{hotelName} rezervasyonunuz ödeme alınamadığı için iptal edildi."),
        _ => null
    };

    public async Task ReservationChangedAsync(Guid hotelId, Guid reservationId, ReservationChange change, CancellationToken ct = default)
    {
        try
        {
            var target = await _db.GuestPushTokens.IgnoreQueryFilters()
                .Where(t => t.ReservationId == reservationId)
                .Select(t => new { t.Id, t.Token, HotelName = t.Hotel.Name, t.Reservation.ConfirmationCode })
                .ToListAsync(ct);
            if (target.Count == 0 || MessageFor(change, target[0].HotelName) is not { } message)
            {
                return;
            }

            var payload = target.Select(t => new ExpoMessage(
                t.Token, message.Title, message.Body,
                new Dictionary<string, string> { ["confirmationCode"] = t.ConfirmationCode ?? "" },
                "reservations")).ToList();

            var response = await _httpClientFactory.CreateClient(HttpClientName)
                .PostAsJsonAsync("https://exp.host/--/api/v2/push/send", payload, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Expo push basarisiz: {Status}", (int)response.StatusCode);
                return;
            }

            // Uygulamasi silinmis telefonlarin token'larini temizle (Expo "DeviceNotRegistered" der)
            var result = await response.Content.ReadFromJsonAsync<ExpoResponse>(ct);
            var dead = result?.Data
                .Select((ticket, i) => (ticket, target[i].Id))
                .Where(x => x.ticket.Details?.Error == "DeviceNotRegistered")
                .Select(x => x.Id)
                .ToList() ?? [];
            if (dead.Count > 0)
            {
                _db.GuestPushTokens.RemoveRange(_db.GuestPushTokens.IgnoreQueryFilters().Where(t => dead.Contains(t.Id)));
                await _db.SaveChangesAsync(ct);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Push bildirim gonderilemedi {ReservationId}", reservationId);
        }
    }

    private record ExpoMessage(
        [property: JsonPropertyName("to")] string To,
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("body")] string Body,
        [property: JsonPropertyName("data")] Dictionary<string, string> Data,
        [property: JsonPropertyName("channelId")] string ChannelId)
    {
        [JsonPropertyName("sound")] public string Sound => "default";
    }

    private record ExpoResponse([property: JsonPropertyName("data")] List<ExpoTicket> Data);

    private record ExpoTicket(
        [property: JsonPropertyName("status")] string Status,
        [property: JsonPropertyName("details")] ExpoTicketDetails? Details);

    private record ExpoTicketDetails([property: JsonPropertyName("error")] string? Error);
}

/// <summary>Birden fazla bildirim kanalini (panel icin SignalR, misafir icin push) tek arayuzde birlestirir.</summary>
public class CompositeReservationNotifier(IEnumerable<IReservationNotifier> channels) : IReservationNotifier
{
    public async Task ReservationChangedAsync(Guid hotelId, Guid reservationId, ReservationChange change, CancellationToken ct = default)
    {
        foreach (var channel in channels)
        {
            await channel.ReservationChangedAsync(hotelId, reservationId, change, ct);
        }
    }
}
