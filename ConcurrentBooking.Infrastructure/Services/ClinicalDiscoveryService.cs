namespace ConcurrentBooking.Infrastructure.Services;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ConcurrentBooking.Application.Clinical;
using ConcurrentBooking.Domain.Entities;
using ConcurrentBooking.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

public sealed class ClinicalDiscoveryService : IClinicalDiscoveryService
{
    private readonly BookingDbContext _db;

    public ClinicalDiscoveryService(BookingDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyCollection<SpecialtyListItem>> GetSpecialtiesAsync(CancellationToken cancellationToken)
    {
        return await _db.Specialties
            .OrderBy(specialty => specialty.Name)
            .Select(specialty => new SpecialtyListItem(specialty.Id, specialty.Name))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<ClinicUnitListItem>> GetUnitsAsync(CancellationToken cancellationToken)
    {
        return await _db.ClinicUnits
            .OrderBy(unit => unit.Name)
            .Select(unit => new ClinicUnitListItem(unit.Id, unit.Name))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<ProfessionalSearchItem>> GetProfessionalsAsync(
        ProfessionalSearchQuery query,
        CancellationToken cancellationToken)
    {
        var dayStart = DateTime.SpecifyKind(query.Date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        var dayEnd = dayStart.AddDays(1);

        var rows =
            from slot in _db.Slots
            join professional in _db.Professionals on slot.ProfessionalId equals professional.Id
            join specialty in _db.Specialties on professional.SpecialtyId equals specialty.Id
            join unit in _db.ClinicUnits on slot.ClinicUnitId equals unit.Id
            where professional.SpecialtyId == query.SpecialtyId
                && slot.StartsAt >= dayStart
                && slot.StartsAt < dayEnd
            select new
            {
                ProfessionalId = professional.Id,
                ProfessionalName = professional.FullName,
                SpecialtyId = specialty.Id,
                SpecialtyName = specialty.Name,
                ClinicUnitId = unit.Id,
                ClinicUnitName = unit.Name
            };

        if (query.UnitId.HasValue)
        {
            rows = rows.Where(row => row.ClinicUnitId == query.UnitId.Value);
        }

        var result = await rows
            .Distinct()
            .OrderBy(row => row.ProfessionalName)
            .ThenBy(row => row.ClinicUnitName)
            .ToListAsync(cancellationToken);

        return result
            .Select(row => new ProfessionalSearchItem(
                row.ProfessionalId,
                row.ProfessionalName,
                row.SpecialtyId,
                row.SpecialtyName,
                row.ClinicUnitId,
                row.ClinicUnitName))
            .ToArray();
    }

    public async Task<IReadOnlyCollection<AvailableSlotItem>> GetAvailabilityAsync(
        ProfessionalAvailabilityQuery query,
        CancellationToken cancellationToken)
    {
        var window = AvailabilityWindow.For(query.Date, query.Period);

        var candidateSlots = await _db.Slots
            .Where(slot =>
                slot.ProfessionalId == query.ProfessionalId &&
                slot.ClinicUnitId == query.ClinicUnitId &&
                slot.StartsAt >= window.StartsAt &&
                slot.StartsAt < window.EndsAt)
            .OrderBy(slot => slot.StartsAt)
            .ToListAsync(cancellationToken);

        if (candidateSlots.Count == 0)
        {
            return Array.Empty<AvailableSlotItem>();
        }

        var nowUtc = DateTime.UtcNow;
        var slotIds = candidateSlots.Select(slot => slot.Id).ToArray();

        var activeHolds = await _db.Holds
            .Where(hold =>
                slotIds.Contains(hold.SlotId) &&
                hold.Status == HoldStatus.Active &&
                hold.ExpiresAt > nowUtc)
            .ToListAsync(cancellationToken);

        var bookings = await _db.Bookings
            .Where(booking =>
                slotIds.Contains(booking.SlotId) &&
                booking.Status == BookingStatus.Confirmed)
            .ToListAsync(cancellationToken);

        return AvailabilityFilter
            .FilterAvailable(candidateSlots, activeHolds, bookings, nowUtc)
            .Select(slot => new AvailableSlotItem(slot.Id, slot.StartsAt, slot.EndsAt, slot.SeatCode))
            .ToArray();
    }
}
