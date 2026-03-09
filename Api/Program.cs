using Api.Services;
using ConcurrentBooking.Application.Clinical;
using ConcurrentBooking.Infrastructure.Data;
using ConcurrentBooking.Infrastructure.Repositories;
using ConcurrentBooking.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Api;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddControllers();

        var conn = builder.Configuration.GetConnectionString("Default")
            ?? "Host=localhost;Database=concurrent_booking;Username=postgres;Password=postgres";

        builder.Services.AddDbContext<BookingDbContext>(options => options.UseNpgsql(conn));

        builder.Services.AddScoped<ConcurrentBooking.Application.Repositories.ISlotRepository, EfSlotRepository>();
        builder.Services.AddScoped<ConcurrentBooking.Application.Repositories.IHoldRepository, EfHoldRepository>();
        builder.Services.AddScoped<ConcurrentBooking.Application.Repositories.IBookingRepository, EfBookingRepository>();
        builder.Services.AddScoped<ConcurrentBooking.Application.Repositories.IIdempotencyRepository, EfIdempotencyRepository>();
        builder.Services.AddScoped<IClinicalDiscoveryService, ClinicalDiscoveryService>();
        builder.Services.AddScoped<IClinicalDemoDataSeeder, ClinicalDemoDataSeeder>();
        builder.Services.AddScoped<IDatabaseInitializer, MigratingDatabaseInitializer>();

        builder.Services.AddTransient<ConcurrentBooking.Application.UseCases.HoldSlotHandler>();
        builder.Services.AddTransient<ConcurrentBooking.Application.UseCases.ConfirmBookingHandler>();
        builder.Services.AddHostedService<HoldExpirationService>();

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        var app = builder.Build();

        await using (var scope = app.Services.CreateAsyncScope())
        {
            var initializer = scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>();
            await initializer.InitializeAsync();
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
        await app.RunAsync();
    }
}
