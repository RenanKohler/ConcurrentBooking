namespace ConcurrentBooking.Domain.Entities;

using System;

public class Slot
{
    public Guid Id { get; init; }

    public Guid ProfessionalId { get; init; }

    public Guid ClinicUnitId { get; init; }

    public DateTime StartsAt { get; init; }

    public DateTime EndsAt { get; init; }

    public string? SeatCode { get; init; }

    public Slot()
    {
        Id = Guid.NewGuid();
    }
}
