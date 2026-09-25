using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StayFlow.Application.Hotels;

namespace StayFlow.API.Controllers;

[ApiController]
[Authorize]
[Route("api/hotel")]
public class HotelController : ControllerBase
{
    private readonly HotelService _hotelService;

    public HotelController(HotelService hotelService)
    {
        _hotelService = hotelService;
    }

    [HttpGet]
    public async Task<ActionResult<HotelDto>> Get(CancellationToken ct) =>
        Ok(await _hotelService.GetCurrentAsync(ct));

    [Authorize(Roles = Roles.Owner)]
    [HttpPut]
    public async Task<ActionResult<HotelDto>> Update(UpdateHotelRequest request, CancellationToken ct) =>
        Ok(await _hotelService.UpdateCurrentAsync(request, ct));
}
