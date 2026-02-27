using Microsoft.EntityFrameworkCore;
using ConcurrentBooking.Infrastructure.Data;

namespace Api
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            builder.Services.AddControllers();
            // Application and infrastructure wiring (Postgres EF Core)
            var conn = builder.Configuration.GetConnectionString("Default") ?? "Host=localhost;Database=concurrent_booking;Username=postgres;Password=postgres";
            builder.Services.AddDbContext<ConcurrentBooking.Infrastructure.Data.BookingDbContext>(options => options.UseNpgsql(conn));

            builder.Services.AddScoped<ConcurrentBooking.Application.Repositories.ISlotRepository, ConcurrentBooking.Infrastructure.Repositories.EfSlotRepository>();
            builder.Services.AddScoped<ConcurrentBooking.Application.Repositories.IHoldRepository, ConcurrentBooking.Infrastructure.Repositories.EfHoldRepository>();
            builder.Services.AddScoped<ConcurrentBooking.Application.Repositories.IBookingRepository, ConcurrentBooking.Infrastructure.Repositories.EfBookingRepository>();
            builder.Services.AddScoped<ConcurrentBooking.Application.Repositories.IIdempotencyRepository, ConcurrentBooking.Infrastructure.Repositories.EfIdempotencyRepository>();

            builder.Services.AddTransient<ConcurrentBooking.Application.UseCases.HoldSlotHandler>();
            builder.Services.AddTransient<ConcurrentBooking.Application.UseCases.ConfirmBookingHandler>();
            builder.Services.AddHostedService<Api.Services.HoldExpirationService>();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            var app = builder.Build();

            // Apply EF Core migrations at startup
            using (var scope = app.Services.CreateScope())
            {
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
                try
                {
                    var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
                    logger.LogInformation("Applying database migrations...");
                    db.Database.Migrate();
                    logger.LogInformation("Database migrations applied.");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to apply database migrations");
                    throw;
                }
            }

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();

            app.UseAuthorization();

            // Idempotency middleware should run before controllers
            app.UseMiddleware<Api.Middleware.IdempotencyMiddleware>();

            app.MapControllers();

            app.Run();
        }
    }
}
