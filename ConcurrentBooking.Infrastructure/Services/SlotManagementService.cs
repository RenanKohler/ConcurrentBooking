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

public sealed class SlotManagementService : ISlotManagementService
{
    private readonly BookingDbContext _db;

    public SlotManagementService(BookingDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyCollection<SlotAdminItem>> ListAsync(SlotQuery query, CancellationToken cancellationToken)
    {
        var slotsQuery =
            from slot in _db.Slots
            join professional in _db.Professionals on slot.ProfessionalId equals professional.Id
            join unit in _db.ClinicUnits on slot.ClinicUnitId equals unit.Id
            select new { slot, professional, unit };

        if (query.ProfessionalId.HasValue)
        {
            slotsQuery = slotsQuery.Where(row => row.slot.ProfessionalId == query.ProfessionalId.Value);
        }

        if (query.ClinicUnitId.HasValue)
        {
            slotsQuery = slotsQuery.Where(row => row.slot.ClinicUnitId == query.ClinicUnitId.Value);
        }

        if (query.Date.HasValue)
        {
            var dayStart = DateTime.SpecifyKind(query.Date.Value.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
            var dayEnd = dayStart.AddDays(1);
            slotsQuery = slotsQuery.Where(row => row.slot.StartsAt >= dayStart && row.slot.StartsAt < dayEnd);
        }

        var rows = await slotsQuery
            .OrderBy(row => row.slot.StartsAt)
            .ThenBy(row => row.professional.FullName)
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return Array.Empty<SlotAdminItem>();
        }

        var slotIds = rows.Select(row => row.slot.Id).ToArray();
        var nowUtc = DateTime.UtcNow;

        var bookedSlotIds = await _db.Bookings
            .Where(b => slotIds.Contains(b.SlotId) && b.Status == BookingStatus.Confirmed)
            .Select(b => b.SlotId)
            .ToListAsync(cancellationToken);

        var heldSlotIds = await _db.Holds
            .Where(h => slotIds.Contains(h.SlotId) && h.Status == HoldStatus.Active && h.ExpiresAt > nowUtc)
            .Select(h => h.SlotId)
            .ToListAsync(cancellationToken);

        var booked = bookedSlotIds.ToHashSet();
        var held = heldSlotIds.ToHashSet();

        return rows
            .Select(row => ToItem(row.slot, row.professional.FullName, row.unit.Name, Status(row.slot.Id, booked, held)))
            .ToArray();
    }

    public async Task<SlotAdminItem?> GetAsync(Guid slotId, CancellationToken cancellationToken)
    {
        var row = await (
            from slot in _db.Slots
            join professional in _db.Professionals on slot.ProfessionalId equals professional.Id
            join unit in _db.ClinicUnits on slot.ClinicUnitId equals unit.Id
            where slot.Id == slotId
            select new { slot, professional, unit })
            .FirstOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            return null;
        }

        var status = await ComputeStatusAsync(slotId, cancellationToken);
        return ToItem(row.slot, row.professional.FullName, row.unit.Name, status);
    }

    public async Task<SlotAdminItem> CreateAsync(CreateSlotInput input, CancellationToken cancellationToken)
    {
        var startUtc = NormalizeUtc(input.StartsAt);
        var duration = ResolveDuration(input.DurationMinutes);

        var professional = await _db.Professionals.FirstOrDefaultAsync(p => p.Id == input.ProfessionalId, cancellationToken)
            ?? throw new KeyNotFoundException("Profissional não encontrado.");
        var unit = await _db.ClinicUnits.FirstOrDefaultAsync(u => u.Id == input.ClinicUnitId, cancellationToken)
            ?? throw new KeyNotFoundException("Unidade não encontrada.");

        var duplicate = await _db.Slots.AnyAsync(
            s => s.ProfessionalId == input.ProfessionalId
                && s.ClinicUnitId == input.ClinicUnitId
                && s.StartsAt == startUtc,
            cancellationToken);
        if (duplicate)
        {
            throw new InvalidOperationException("Já existe um horário para este profissional, unidade e horário de início.");
        }

        var slot = new Slot
        {
            ProfessionalId = input.ProfessionalId,
            ClinicUnitId = input.ClinicUnitId,
            StartsAt = startUtc,
            EndsAt = startUtc.AddMinutes(duration),
            SeatCode = string.IsNullOrWhiteSpace(input.SeatCode) ? null : input.SeatCode.Trim()
        };

        _db.Slots.Add(slot);
        await _db.SaveChangesAsync(cancellationToken);

        return ToItem(slot, professional.FullName, unit.Name, "Available");
    }

    public async Task<SlotAdminItem> UpdateAsync(Guid slotId, UpdateSlotInput input, CancellationToken cancellationToken)
    {
        var slot = await _db.Slots.FirstOrDefaultAsync(s => s.Id == slotId, cancellationToken)
            ?? throw new KeyNotFoundException("Horário não encontrado.");

        var hasBooking = await _db.Bookings.AnyAsync(
            b => b.SlotId == slotId && b.Status == BookingStatus.Confirmed, cancellationToken);
        if (hasBooking)
        {
            throw new InvalidOperationException("Não é possível editar um horário que já possui reserva confirmada.");
        }

        var startUtc = NormalizeUtc(input.StartsAt);
        var duration = ResolveDuration(input.DurationMinutes);

        var duplicate = await _db.Slots.AnyAsync(
            s => s.Id != slotId
                && s.ProfessionalId == slot.ProfessionalId
                && s.ClinicUnitId == slot.ClinicUnitId
                && s.StartsAt == startUtc,
            cancellationToken);
        if (duplicate)
        {
            throw new InvalidOperationException("Já existe outro horário para este profissional, unidade e horário de início.");
        }

        slot.StartsAt = startUtc;
        slot.EndsAt = startUtc.AddMinutes(duration);
        slot.SeatCode = string.IsNullOrWhiteSpace(input.SeatCode) ? null : input.SeatCode.Trim();

        await _db.SaveChangesAsync(cancellationToken);

        var professional = await _db.Professionals.FirstAsync(p => p.Id == slot.ProfessionalId, cancellationToken);
        var unit = await _db.ClinicUnits.FirstAsync(u => u.Id == slot.ClinicUnitId, cancellationToken);
        var status = await ComputeStatusAsync(slotId, cancellationToken);

        return ToItem(slot, professional.FullName, unit.Name, status);
    }

    public async Task DeleteAsync(Guid slotId, CancellationToken cancellationToken)
    {
        var slot = await _db.Slots.FirstOrDefaultAsync(s => s.Id == slotId, cancellationToken)
            ?? throw new KeyNotFoundException("Horário não encontrado.");

        var hasBooking = await _db.Bookings.AnyAsync(
            b => b.SlotId == slotId && b.Status == BookingStatus.Confirmed, cancellationToken);
        if (hasBooking)
        {
            throw new InvalidOperationException("Não é possível excluir um horário que já possui reserva confirmada.");
        }

        // Any non-confirmed holds cascade-delete via the FK relationship.
        _db.Slots.Remove(slot);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<string> ComputeStatusAsync(Guid slotId, CancellationToken cancellationToken)
    {
        if (await _db.Bookings.AnyAsync(b => b.SlotId == slotId && b.Status == BookingStatus.Confirmed, cancellationToken))
        {
            return "Booked";
        }

        var nowUtc = DateTime.UtcNow;
        if (await _db.Holds.AnyAsync(h => h.SlotId == slotId && h.Status == HoldStatus.Active && h.ExpiresAt > nowUtc, cancellationToken))
        {
            return "Held";
        }

        return "Available";
    }

    private static string Status(Guid slotId, HashSet<Guid> booked, HashSet<Guid> held)
    {
        if (booked.Contains(slotId)) return "Booked";
        if (held.Contains(slotId)) return "Held";
        return "Available";
    }

    private static SlotAdminItem ToItem(Slot slot, string professionalName, string unitName, string status) =>
        new(slot.Id, slot.ProfessionalId, professionalName, slot.ClinicUnitId, unitName,
            slot.StartsAt, slot.EndsAt, slot.SeatCode, status);

    private static DateTime NormalizeUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    private static int ResolveDuration(int durationMinutes)
    {
        if (durationMinutes <= 0)
        {
            durationMinutes = 30;
        }

        if (durationMinutes > 24 * 60)
        {
            throw new ArgumentException("Duração inválida.");
        }

        return durationMinutes;
    }
}
