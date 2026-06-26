using ConcurrentBooking.Application.Clinical;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api")]
public sealed class SlotsController : ControllerBase
{
    private readonly ISlotManagementService _slots;

    public SlotsController(ISlotManagementService slots)
    {
        _slots = slots;
    }

    [HttpGet("slots")]
    public async Task<ActionResult<IReadOnlyCollection<SlotAdminItem>>> List(
        [FromQuery] Guid? professionalId,
        [FromQuery] Guid? unitId,
        [FromQuery] DateOnly? date,
        CancellationToken cancellationToken)
    {
        var result = await _slots.ListAsync(new SlotQuery(professionalId, unitId, date), cancellationToken);
        return Ok(result);
    }

    [HttpGet("slots/{slotId:guid}")]
    public async Task<ActionResult<SlotAdminItem>> Get([FromRoute] Guid slotId, CancellationToken cancellationToken)
    {
        var slot = await _slots.GetAsync(slotId, cancellationToken);
        return slot is null ? NotFound(new { error = "Horário não encontrado." }) : Ok(slot);
    }

    [HttpPost("slots")]
    public async Task<ActionResult<SlotAdminItem>> Create([FromBody] CreateSlotApiRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var created = await _slots.CreateAsync(
                new CreateSlotInput(
                    request.ProfessionalId,
                    request.ClinicUnitId,
                    request.StartsAt,
                    request.DurationMinutes ?? 0,
                    request.SeatCode),
                cancellationToken);

            return CreatedAtAction(nameof(Get), new { slotId = created.SlotId }, created);
        }
        catch (Exception ex)
        {
            return MapError(ex);
        }
    }

    [HttpPut("slots/{slotId:guid}")]
    public async Task<ActionResult<SlotAdminItem>> Update(
        [FromRoute] Guid slotId,
        [FromBody] UpdateSlotApiRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var updated = await _slots.UpdateAsync(
                slotId,
                new UpdateSlotInput(request.StartsAt, request.DurationMinutes ?? 0, request.SeatCode),
                cancellationToken);

            return Ok(updated);
        }
        catch (Exception ex)
        {
            return MapError(ex);
        }
    }

    [HttpDelete("slots/{slotId:guid}")]
    public async Task<IActionResult> Delete([FromRoute] Guid slotId, CancellationToken cancellationToken)
    {
        try
        {
            await _slots.DeleteAsync(slotId, cancellationToken);
            return NoContent();
        }
        catch (Exception ex)
        {
            return MapError(ex);
        }
    }

    private ActionResult MapError(Exception ex) => ex switch
    {
        KeyNotFoundException => NotFound(new { error = ex.Message }),
        InvalidOperationException => Conflict(new { error = ex.Message }),
        ArgumentException => BadRequest(new { error = ex.Message }),
        _ => StatusCode(500, new { error = "Erro inesperado." })
    };
}

public sealed record CreateSlotApiRequest(
    Guid ProfessionalId,
    Guid ClinicUnitId,
    DateTime StartsAt,
    int? DurationMinutes,
    string? SeatCode);

public sealed record UpdateSlotApiRequest(
    DateTime StartsAt,
    int? DurationMinutes,
    string? SeatCode);
