namespace ConcurrentBooking.Domain.Entities;

using System;

public enum BookingStatus { Confirmed, Cancelled }

public class Booking
{
    public Guid Id { get; init; }

    public Guid SlotId { get; init; }

    public Guid CustomerId { get; init; }

    public Guid HoldId { get; init; }

    public BookingStatus Status { get; private set; }

    public DateTime CreatedAt { get; init; }

    public string? RequestId { get; init; }

    public Booking(Guid slotId, Guid customerId, Guid holdId, string? requestId = null)
    {
        Id = Guid.NewGuid();
        SlotId = slotId;
        CustomerId = customerId;
        HoldId = holdId;
        CreatedAt = DateTime.UtcNow;
        Status = BookingStatus.Confirmed;
        RequestId = requestId;
    }

    public void Cancel()
    {
        if (Status != BookingStatus.Confirmed) throw new InvalidOperationException("Booking not confirmed");
        Status = BookingStatus.Cancelled;
    }
}
