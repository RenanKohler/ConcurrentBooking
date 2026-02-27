using System;

namespace ConcurrentBooking.Application.Dtos
{
    public record HoldSlotRequest(Guid SlotId, Guid CustomerId, string IdempotencyKey);

    public record HoldSlotResult(Guid HoldId, DateTime ExpiresAt);

    public record ConfirmBookingRequest(Guid HoldId, Guid CustomerId, string IdempotencyKey);

    public record ConfirmBookingResult(Guid BookingId);
}
