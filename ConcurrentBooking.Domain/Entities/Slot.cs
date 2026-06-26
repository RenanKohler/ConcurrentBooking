namespace ConcurrentBooking.Domain.Entities;

using System;

public class Slot
{
    public Guid Id { get; init; }

    public Guid ProfessionalId { get; init; }

    public Guid ClinicUnitId { get; init; }

    // Settable so slots can be rescheduled/edited in place (CRUD update).
    public DateTime StartsAt { get; set; }

    public DateTime EndsAt { get; set; }

    public string? SeatCode { get; set; }

    public Slot()
    {
        Id = Guid.NewGuid();
    }
}
