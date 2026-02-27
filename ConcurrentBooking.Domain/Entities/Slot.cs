namespace ConcurrentBooking.Domain.Entities;

using System;

public class Slot
{
    public Guid Id { get; init; }

    public Guid ResourceId { get; init; }

    public DateTime StartsAt { get; init; }

    public DateTime EndsAt { get; init; }

    public string? SeatCode { get; init; }

    // A slot is available when it has no active booking
    public bool IsAvailable => true; // availability resolved by repository/DB constraints

    public Slot()
    {
        Id = Guid.NewGuid();
    }
}
