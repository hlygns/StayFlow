using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StayFlow.Application.Rooms;

namespace StayFlow.API.Controllers;

[ApiController]
[Authorize(Roles = Roles.Staff)]
[Route("api/rooms")]
public class RoomsController : ControllerBase
{
    private readonly RoomService _roomService;

    public RoomsController(RoomService roomService)
    {
        _roomService = roomService;
    }

    [HttpGet]
    public async Task<ActionResult<List<RoomDto>>> GetAll(
        [FromQuery] Guid? roomTypeId, [FromQuery] bool includeInactive, CancellationToken ct) =>
        Ok(await _roomService.GetAllAsync(roomTypeId, includeInactive, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RoomDto>> Get(Guid id, CancellationToken ct) =>
        Ok(await _roomService.GetAsync(id, ct));

    [Authorize(Roles = Roles.Owner)]
    [HttpPost]
    public async Task<ActionResult<RoomDto>> Create(SaveRoomRequest request, CancellationToken ct)
    {
        var room = await _roomService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = room.Id }, room);
    }

    [Authorize(Roles = Roles.Owner)]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<RoomDto>> Update(Guid id, SaveRoomRequest request, CancellationToken ct) =>
        Ok(await _roomService.UpdateAsync(id, request, ct));

    /// <summary>Odayi pasife alir (soft delete).</summary>
    [Authorize(Roles = Roles.Owner)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        await _roomService.DeactivateAsync(id, ct);
        return NoContent();
    }
}
