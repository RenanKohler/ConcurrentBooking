namespace ConcurrentBooking.Application.Clinical;

using System;

// Filters for listing slots in the management/CRUD surface.
public sealed record SlotQuery(Guid? ProfessionalId, Guid? ClinicUnitId, DateOnly? Date);

// Input to create a new slot. DurationMinutes defaults are applied by the service.
public sealed record CreateSlotInput(
    Guid ProfessionalId,
    Guid ClinicUnitId,
    DateTime StartsAt,
    int DurationMinutes,
    string? SeatCode);

// Input to update an existing slot's schedule/seat (professional/unit are fixed).
public sealed record UpdateSlotInput(
    DateTime StartsAt,
    int DurationMinutes,
    string? SeatCode);

// A slot as shown in the management list, enriched with names and live status.
public sealed record SlotAdminItem(
    Guid SlotId,
    Guid ProfessionalId,
    string ProfessionalName,
    Guid ClinicUnitId,
    string ClinicUnitName,
    DateTime StartsAt,
    DateTime EndsAt,
    string? SeatCode,
    string Status);
