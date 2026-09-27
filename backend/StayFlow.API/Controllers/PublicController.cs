using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using StayFlow.API.Infrastructure;
using StayFlow.Application.Public;

namespace StayFlow.API.Controllers;

/// <summary>
/// Misafir (mobil) uygulamasinin uclari. Oturum yok; otel slug ile, rezervasyon "kod + e-posta" ile secilir.
/// </summary>
[ApiController]
[AllowAnonymous]
[EnableRateLimiting(RateLimitPolicies.Public)]
[Route("api/public")]
public class PublicController : ControllerBase
{
    private readonly PublicBookingService _service;

    public PublicController(PublicBookingService service)
    {
        _service = service;
    }

    [HttpGet("hotels")]
    public async Task<ActionResult<List<PublicHotelSummary>>> Hotels([FromQuery] string? city, CancellationToken ct) =>
        Ok(await _service.ListHotelsAsync(city, ct));

    [HttpGet("hotels/{slug}")]
    public async Task<ActionResult<PublicHotelDetail>> Hotel(string slug, CancellationToken ct) =>
        Ok(await _service.GetHotelAsync(slug, ct));

    [HttpGet("hotels/{slug}/availability")]
    public async Task<ActionResult<List<PublicAvailability>>> Availability(
        string slug, [FromQuery] DateOnly checkIn, [FromQuery] DateOnly checkOut, [FromQuery] int? guests, CancellationToken ct) =>
        Ok(await _service.SearchAsync(slug, checkIn, checkOut, guests, ct));

    [HttpPost("hotels/{slug}/reservations")]
    public async Task<ActionResult<GuestReservation>> Book(string slug, PublicBookingRequest request, CancellationToken ct)
    {
        var reservation = await _service.BookAsync(slug, request, ct);
        return CreatedAtAction(nameof(Reservation), new { code = reservation.ConfirmationCode }, reservation);
    }

    /// <summary>E-posta sorgu parametresinde degil govdede: adres satirindaki veri loglara ve gecmise duser.</summary>
    [HttpPost("reservations/{code}")]
    public async Task<ActionResult<GuestReservation>> Reservation(string code, GuestAccessRequest request, CancellationToken ct) =>
        Ok(await _service.GetAsync(code, request.Email, ct));

    [HttpPost("reservations/{code}/pay")]
    public async Task<ActionResult<GuestReservation>> Pay(string code, SimulatedPaymentRequest request, CancellationToken ct) =>
        Ok(await _service.PayAsync(code, request, ct));

    [HttpPost("reservations/{code}/cancel")]
    public async Task<ActionResult<GuestReservation>> Cancel(string code, GuestAccessRequest request, CancellationToken ct) =>
        Ok(await _service.CancelAsync(code, request.Email, ct));
}
