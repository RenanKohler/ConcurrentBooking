namespace ConcurrentBooking.Application.Clinical;

using System;

public sealed record SpecialtyListItem(Guid Id, string Name);

public sealed record ClinicUnitListItem(Guid Id, string Name);

public sealed record ProfessionalSearchQuery(Guid SpecialtyId, DateOnly Date, Guid? UnitId);

public sealed record ProfessionalSearchItem(
    Guid ProfessionalId,
    string ProfessionalName,
    Guid SpecialtyId,
    string SpecialtyName,
    Guid ClinicUnitId,
    string ClinicUnitName);

public sealed record ProfessionalAvailabilityQuery(
    Guid ProfessionalId,
    DateOnly Date,
    AvailabilityPeriod Period,
    Guid ClinicUnitId);

public sealed record AvailableSlotItem(
    Guid SlotId,
    DateTime StartsAt,
    DateTime EndsAt,
    string? SeatCode);
