namespace Api.Services;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ConcurrentBooking.Domain.Entities;
using ConcurrentBooking.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

public sealed class ClinicalDemoDataSeeder : IClinicalDemoDataSeeder
{
    private readonly ILogger<ClinicalDemoDataSeeder> _logger;

    public ClinicalDemoDataSeeder(ILogger<ClinicalDemoDataSeeder> logger)
    {
        _logger = logger;
    }

    public async Task SeedAsync(BookingDbContext db, CancellationToken cancellationToken = default)
    {
        if (await db.Specialties.AnyAsync(cancellationToken))
        {
            _logger.LogInformation("Clinical seed skipped because data already exists.");
            return;
        }

        var specialties = new[]
        {
            new Specialty("Clinica Geral"),
            new Specialty("Cardiologia"),
            new Specialty("Pediatria"),
            new Specialty("Ortopedia"),
            new Specialty("Dermatologia")
        };

        var units = new[]
        {
            new ClinicUnit("Unidade Centro"),
            new ClinicUnit("Unidade Zona Sul"),
            new ClinicUnit("Unidade Zona Norte")
        };

        var professionals = new[]
        {
            new Professional(specialties[0].Id, "Dra. Ana Costa"),
            new Professional(specialties[1].Id, "Dr. Bruno Lima"),
            new Professional(specialties[2].Id, "Dra. Carla Souza"),
            new Professional(specialties[3].Id, "Dr. Diego Rocha"),
            new Professional(specialties[4].Id, "Dra. Elisa Martins")
        };

        db.Specialties.AddRange(specialties);
        db.ClinicUnits.AddRange(units);
        db.Professionals.AddRange(professionals);

        var schedules = new (Professional Professional, ClinicUnit[] Units, int[] Hours)[]
        {
            (professionals[0], new[] { units[0] }, new[] { 8, 9, 14, 15 }),
            (professionals[1], new[] { units[0], units[1] }, new[] { 9, 10, 18 }),
            (professionals[2], new[] { units[2] }, new[] { 8, 9, 14 }),
            (professionals[3], new[] { units[1] }, new[] { 12, 13, 16 }),
            (professionals[4], new[] { units[0], units[2] }, new[] { 15, 18, 19 })
        };

        var slots = new List<Slot>();
        var baseDate = DateTime.UtcNow.Date.AddDays(1);

        foreach (var schedule in schedules)
        {
            for (var dayOffset = 0; dayOffset < 7; dayOffset++)
            {
                var date = baseDate.AddDays(dayOffset);

                foreach (var unit in schedule.Units)
                {
                    foreach (var hour in schedule.Hours)
                    {
                        var startsAt = date.AddHours(hour);
                        slots.Add(new Slot
                        {
                            ProfessionalId = schedule.Professional.Id,
                            ClinicUnitId = unit.Id,
                            StartsAt = startsAt,
                            EndsAt = startsAt.AddMinutes(30),
                            SeatCode = $"C{hour:00}-{dayOffset + 1:00}"
                        });
                    }
                }
            }
        }

        db.Slots.AddRange(slots);
        await db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Clinical seed completed with {ProfessionalCount} professionals and {SlotCount} slots.",
            professionals.Length,
            slots.Count);
    }
}
