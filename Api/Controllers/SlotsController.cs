using ConcurrentBooking.Domain.Entities;
using ConcurrentBooking.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers
{
    [ApiController]
    [Route("api/slots")]
    public class SlotsController : ControllerBase
    {
        private readonly BookingDbContext _db;

        public SlotsController(BookingDbContext db)
        {
            _db = db;
        }

        [HttpGet]
        public async Task<ActionResult<IReadOnlyCollection<SlotDto>>> GetAll()
        {
            var slots = await (
                from s in _db.Slots
                join r in _db.Resources on s.ResourceId equals r.Id
                orderby s.StartsAt
                select new SlotDto(
                    s.Id,
                    s.ResourceId,
                    r.Name,
                    s.StartsAt,
                    s.EndsAt,
                    s.SeatCode))
                .ToListAsync();

            return Ok(slots);
        }

        [HttpGet("{slotId:guid}")]
        public async Task<ActionResult<SlotDto>> GetById(Guid slotId)
        {
            var slot = await (
                from s in _db.Slots
                join r in _db.Resources on s.ResourceId equals r.Id
                where s.Id == slotId
                select new SlotDto(
                    s.Id,
                    s.ResourceId,
                    r.Name,
                    s.StartsAt,
                    s.EndsAt,
                    s.SeatCode))
                .FirstOrDefaultAsync();

            if (slot is null)
                return NotFound(new { error = "Slot not found" });

            return Ok(slot);
        }

        [HttpPost]
        public async Task<ActionResult<SlotDto>> Create([FromBody] CreateSlotRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.ResourceName))
                return BadRequest(new { error = "ResourceName is required." });

            if (request.EndsAt <= request.StartsAt)
                return BadRequest(new { error = "EndsAt must be greater than StartsAt." });

            var resourceName = request.ResourceName.Trim();

            var resource = await _db.Resources
                .FirstOrDefaultAsync(r => r.Name.ToLower() == resourceName.ToLower());

            if (resource is null)
            {
                resource = new Resource(resourceName);
                _db.Resources.Add(resource);
            }

            var slot = new Slot
            {
                ResourceId = resource.Id,
                StartsAt = request.StartsAt,
                EndsAt = request.EndsAt,
                SeatCode = string.IsNullOrWhiteSpace(request.SeatCode) ? null : request.SeatCode.Trim()
            };

            _db.Slots.Add(slot);
            await _db.SaveChangesAsync();

            var dto = new SlotDto(slot.Id, slot.ResourceId, resource.Name, slot.StartsAt, slot.EndsAt, slot.SeatCode);

            return CreatedAtAction(nameof(GetById), new { slotId = slot.Id }, dto);
        }
    }

    public sealed record CreateSlotRequest(string ResourceName, DateTime StartsAt, DateTime EndsAt, string? SeatCode);

    public sealed record SlotDto(
        Guid Id,
        Guid ResourceId,
        string ResourceName,
        DateTime StartsAt,
        DateTime EndsAt,
        string? SeatCode);
}