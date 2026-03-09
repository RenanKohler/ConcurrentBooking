using ConcurrentBooking.Domain.Entities;
using ConcurrentBooking.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Api
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddControllers();

            var conn = builder.Configuration.GetConnectionString("Default")
                ?? "Host=localhost;Database=concurrent_booking;Username=postgres;Password=postgres";

            builder.Services.AddDbContext<BookingDbContext>(options => options.UseNpgsql(conn));

            builder.Services.AddScoped<ConcurrentBooking.Application.Repositories.ISlotRepository, ConcurrentBooking.Infrastructure.Repositories.EfSlotRepository>();
            builder.Services.AddScoped<ConcurrentBooking.Application.Repositories.IHoldRepository, ConcurrentBooking.Infrastructure.Repositories.EfHoldRepository>();
            builder.Services.AddScoped<ConcurrentBooking.Application.Repositories.IBookingRepository, ConcurrentBooking.Infrastructure.Repositories.EfBookingRepository>();
            builder.Services.AddScoped<ConcurrentBooking.Application.Repositories.IIdempotencyRepository, ConcurrentBooking.Infrastructure.Repositories.EfIdempotencyRepository>();

            builder.Services.AddTransient<ConcurrentBooking.Application.UseCases.HoldSlotHandler>();
            builder.Services.AddTransient<ConcurrentBooking.Application.UseCases.ConfirmBookingHandler>();
            builder.Services.AddHostedService<Api.Services.HoldExpirationService>();

            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            var app = builder.Build();

            using (var scope = app.Services.CreateScope())
            {
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
                try
                {
                    var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();

                    logger.LogInformation("Applying database migrations...");
                    db.Database.Migrate();
                    logger.LogInformation("Database migrations applied.");

                    SeedInitialSlots(db, logger);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed during database startup.");
                    throw;
                }
            }

            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();
            app.UseAuthorization();
            app.UseMiddleware<Api.Middleware.IdempotencyMiddleware>();
            app.MapControllers();
            app.Run();
        }

        private static void SeedInitialSlots(BookingDbContext db, ILogger logger)
        {
            if (db.Slots.Any())
            {
                logger.LogInformation("Slots already exist. Seed skipped.");
                return;
            }

            var doctors = new[]
            {
                "Dra. Ana Costa - Clínica Geral",
                "Dr. Bruno Lima - Cardiologia",
                "Dra. Carla Souza - Pediatria",
                "Dr. Diego Rocha - Ortopedia",
                "Dra. Elisa Martins - Dermatologia"
            };

            var baseStart = DateTime.UtcNow.Date.AddDays(1).AddHours(8);

            var resources = doctors.Select(d => new Resource(d)).ToList();
            var slots = new List<Slot>();

            for (var i = 0; i < resources.Count; i++)
            {
                var startsAt = baseStart.AddHours(i);
                slots.Add(new Slot
                {
                    ResourceId = resources[i].Id,
                    StartsAt = startsAt,
                    EndsAt = startsAt.AddMinutes(30),
                    SeatCode = $"S{i + 1:00}"
                });
            }

            db.Resources.AddRange(resources);
            db.Slots.AddRange(slots);
            db.SaveChanges();

            logger.LogInformation("Seed completed with {Count} slots.", slots.Count);
        }
    }
}
