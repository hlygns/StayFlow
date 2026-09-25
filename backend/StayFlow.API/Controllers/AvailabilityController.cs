using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StayFlow.Application.Reservations;

namespace StayFlow.API.Controllers;

[ApiController]
[Authorize(Roles = Roles.Staff)]
[Route("api/availability")]
public class AvailabilityController : ControllerBase
{
    private readonly AvailabilityService _availabilityService;

    public AvailabilityController(AvailabilityService availabilityService)
    {
        _availabilityService = availabilityService;
    }

    /// <summary>Ornek: GET /api/availability?checkIn=2027-07-10&amp;checkOut=2027-07-13&amp;guests=2</summary>
    [HttpGet]
    public async Task<ActionResult<List<RoomTypeAvailabilityDto>>> Search(
        [FromQuery] DateOnly checkIn, [FromQuery] DateOnly checkOut, [FromQuery] int? guests, CancellationToken ct) =>
        Ok(await _availabilityService.SearchAsync(checkIn, checkOut, guests, ct));
}
