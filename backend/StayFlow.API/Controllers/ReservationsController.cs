using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StayFlow.Application.Reservations;
using StayFlow.Domain.Enums;

namespace StayFlow.API.Controllers;

[ApiController]
[Authorize(Roles = Roles.Staff)]
[Route("api/reservations")]
public class ReservationsController : ControllerBase
{
    private readonly ReservationService _reservationService;

    public ReservationsController(ReservationService reservationService)
    {
        _reservationService = reservationService;
    }

    [HttpGet]
    public async Task<ActionResult<List<ReservationDto>>> GetAll(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] ReservationStatus? status, CancellationToken ct) =>
        Ok(await _reservationService.GetAllAsync(from, to, status, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ReservationDto>> Get(Guid id, CancellationToken ct) =>
        Ok(await _reservationService.GetAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<ReservationDto>> Create(CreateReservationRequest request, CancellationToken ct)
    {
        var reservation = await _reservationService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = reservation.Id }, reservation);
    }

    [HttpPost("{id:guid}/confirm")]
    public async Task<ActionResult<ReservationDto>> Confirm(Guid id, CancellationToken ct) =>
        Ok(await _reservationService.ConfirmAsync(id, ct));

    [HttpPost("{id:guid}/check-in")]
    public async Task<ActionResult<ReservationDto>> CheckIn(Guid id, CancellationToken ct) =>
        Ok(await _reservationService.CheckInAsync(id, ct));

    [HttpPost("{id:guid}/check-out")]
    public async Task<ActionResult<ReservationDto>> CheckOut(Guid id, CancellationToken ct) =>
        Ok(await _reservationService.CheckOutAsync(id, ct));

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<ReservationDto>> Cancel(Guid id, CancellationToken ct) =>
        Ok(await _reservationService.CancelAsync(id, ct));
}
