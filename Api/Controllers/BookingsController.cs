using Microsoft.AspNetCore.Mvc;
using ConcurrentBooking.Application.UseCases;
using ConcurrentBooking.Application.Dtos;

namespace Api.Controllers
{
    [ApiController]
    [Route("api")]
    public class BookingsController : ControllerBase
    {
        private readonly HoldSlotHandler _holdHandler;
        private readonly ConfirmBookingHandler _confirmHandler;

        public BookingsController(HoldSlotHandler holdHandler, ConfirmBookingHandler confirmHandler)
        {
            _holdHandler = holdHandler;
            _confirmHandler = confirmHandler;
        }

        [HttpPost("slots/{slotId:guid}/hold")]
        public async Task<IActionResult> CreateHold([FromRoute] Guid slotId, [FromBody] CreateHoldRequest request)
        {
            var cmd = new HoldSlotRequest(slotId, request.CustomerId, request.IdempotencyKey);
            try
            {
                var result = await _holdHandler.Handle(cmd);
                return CreatedAtAction(null, new { holdId = result.HoldId, expiresAt = result.ExpiresAt });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { error = ex.Message });
            }
        }

        [HttpPost("holds/{holdId:guid}/confirm")]
        public async Task<IActionResult> Confirm([FromRoute] Guid holdId, [FromBody] ConfirmRequest request)
        {
            var cmd = new ConfirmBookingRequest(holdId, request.CustomerId, request.IdempotencyKey);
            try
            {
                var result = await _confirmHandler.Handle(cmd);
                return CreatedAtAction(null, new { bookingId = result.BookingId });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { error = ex.Message });
            }
        }
    }

    public record CreateHoldRequest(Guid CustomerId, string IdempotencyKey);
    public record ConfirmRequest(Guid CustomerId, string IdempotencyKey);
}
