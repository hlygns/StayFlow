using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using StayFlow.Application.Common.Interfaces;

namespace StayFlow.API.Realtime;

/// <summary>
/// Panelin canli baglantisi (WebSocket). Istemci sadece dinler; sunucu rezervasyon degisince haber verir.
/// Her baglanti, JWT'deki hotelId ile kendi otelinin grubuna eklenir: bir otel, baska otelin
/// olaylarini hicbir sekilde alamaz (grup adi istemciden degil, imzali token'dan gelir).
/// </summary>
[Authorize]
public class ReservationsHub : Hub
{
    public const string Path = "/hubs/reservations";
    public const string ReservationChangedEvent = "reservationChanged";

    public static string HotelGroup(Guid hotelId) => $"hotel:{hotelId}";

    public override async Task OnConnectedAsync()
    {
        if (Guid.TryParse(Context.User?.FindFirst("hotelId")?.Value, out var hotelId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, HotelGroup(hotelId));
        }
        else
        {
            Context.Abort();
        }

        await base.OnConnectedAsync();
    }
}

public record ReservationChangedMessage(Guid ReservationId, ReservationChange Change);

/// <summary>Application katmanindaki IReservationNotifier'in SignalR uygulamasi.</summary>
public class SignalRReservationNotifier : IReservationNotifier
{
    private readonly IHubContext<ReservationsHub> _hub;
    private readonly ILogger<SignalRReservationNotifier> _logger;

    public SignalRReservationNotifier(IHubContext<ReservationsHub> hub, ILogger<SignalRReservationNotifier> logger)
    {
        _hub = hub;
        _logger = logger;
    }

    public async Task ReservationChangedAsync(Guid hotelId, Guid reservationId, ReservationChange change, CancellationToken ct = default)
    {
        try
        {
            await _hub.Clients.Group(ReservationsHub.HotelGroup(hotelId))
                .SendAsync(ReservationsHub.ReservationChangedEvent, new ReservationChangedMessage(reservationId, change), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Bildirim gonderilemedi diye rezervasyon islemi basarisiz sayilmasin: veri zaten kaydedildi,
            // panel bir sonraki yenilemede zaten guncel veriyi gorur.
            _logger.LogWarning(ex, "Canli bildirim gonderilemedi {ReservationId}", reservationId);
        }
    }
}
