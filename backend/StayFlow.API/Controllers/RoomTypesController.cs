using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StayFlow.Application.Rooms;

namespace StayFlow.API.Controllers;

/// <summary>Okuma tum personele acik, degisiklik sadece otel sahibine.</summary>
[ApiController]
[Authorize(Roles = Roles.Staff)]
[Route("api/room-types")]
public class RoomTypesController : ControllerBase
{
    private readonly RoomTypeService _roomTypeService;
    private readonly SeasonalPriceService _seasonalPriceService;

    public RoomTypesController(RoomTypeService roomTypeService, SeasonalPriceService seasonalPriceService)
    {
        _roomTypeService = roomTypeService;
        _seasonalPriceService = seasonalPriceService;
    }

    [HttpGet]
    public async Task<ActionResult<List<RoomTypeDto>>> GetAll(CancellationToken ct) =>
        Ok(await _roomTypeService.GetAllAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RoomTypeDto>> Get(Guid id, CancellationToken ct) =>
        Ok(await _roomTypeService.GetAsync(id, ct));

    [Authorize(Roles = Roles.Owner)]
    [HttpPost]
    public async Task<ActionResult<RoomTypeDto>> Create(SaveRoomTypeRequest request, CancellationToken ct)
    {
        var roomType = await _roomTypeService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = roomType.Id }, roomType);
    }

    [Authorize(Roles = Roles.Owner)]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<RoomTypeDto>> Update(Guid id, SaveRoomTypeRequest request, CancellationToken ct) =>
        Ok(await _roomTypeService.UpdateAsync(id, request, ct));

    [Authorize(Roles = Roles.Owner)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _roomTypeService.DeleteAsync(id, ct);
        return NoContent();
    }

    // === Sezon fiyatlari: /api/room-types/{roomTypeId}/seasonal-prices ===

    [HttpGet("{roomTypeId:guid}/seasonal-prices")]
    public async Task<ActionResult<List<SeasonalPriceDto>>> GetPrices(Guid roomTypeId, CancellationToken ct) =>
        Ok(await _seasonalPriceService.GetAllAsync(roomTypeId, ct));

    [Authorize(Roles = Roles.Owner)]
    [HttpPost("{roomTypeId:guid}/seasonal-prices")]
    public async Task<ActionResult<SeasonalPriceDto>> CreatePrice(Guid roomTypeId, SaveSeasonalPriceRequest request, CancellationToken ct)
    {
        var price = await _seasonalPriceService.CreateAsync(roomTypeId, request, ct);
        return CreatedAtAction(nameof(GetPrices), new { roomTypeId }, price);
    }

    [Authorize(Roles = Roles.Owner)]
    [HttpPut("{roomTypeId:guid}/seasonal-prices/{id:guid}")]
    public async Task<ActionResult<SeasonalPriceDto>> UpdatePrice(Guid roomTypeId, Guid id, SaveSeasonalPriceRequest request, CancellationToken ct) =>
        Ok(await _seasonalPriceService.UpdateAsync(roomTypeId, id, request, ct));

    [Authorize(Roles = Roles.Owner)]
    [HttpDelete("{roomTypeId:guid}/seasonal-prices/{id:guid}")]
    public async Task<IActionResult> DeletePrice(Guid roomTypeId, Guid id, CancellationToken ct)
    {
        await _seasonalPriceService.DeleteAsync(roomTypeId, id, ct);
        return NoContent();
    }
}
