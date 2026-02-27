namespace ConcurrentBooking.Domain.Entities;

using System;

public enum HoldStatus { Active, Consumed, Expired }

public class Hold
{
    public Guid Id { get; init; }

    public Guid SlotId { get; init; }

    public Guid CustomerId { get; init; }

    public DateTime ExpiresAt { get; private set; }

    public HoldStatus Status { get; private set; }

    public DateTime CreatedAt { get; init; }

    public string? RequestId { get; init; }

    public Hold(Guid slotId, Guid customerId, TimeSpan ttl, string? requestId = null)
    {
        Id = Guid.NewGuid();
        SlotId = slotId;
        CustomerId = customerId;
        ExpiresAt = DateTime.UtcNow.Add(ttl);
        CreatedAt = DateTime.UtcNow;
        Status = HoldStatus.Active;
        RequestId = requestId;
    }

    // Constructor used by EF Core when materializing entities. It matches mapped properties
    // so EF can bind values directly to parameters even though the public domain
    // constructor uses TTL semantics.
    private Hold(Guid id, Guid slotId, Guid customerId, DateTime expiresAt, HoldStatus status, DateTime createdAt, string? requestId)
    {
        Id = id;
        SlotId = slotId;
        CustomerId = customerId;
        ExpiresAt = expiresAt;
        Status = status;
        CreatedAt = createdAt;
        RequestId = requestId;
    }

    public bool IsExpired() => DateTime.UtcNow >= ExpiresAt;

    public void MarkConsumed()
    {
        if (Status != HoldStatus.Active) throw new InvalidOperationException("Hold is not active");
        Status = HoldStatus.Consumed;
    }

    public void MarkExpired()
    {
        Status = HoldStatus.Expired;
    }
}
