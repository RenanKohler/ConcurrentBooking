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

        // CORS so a separately hosted web client (e.g. Netlify) can call this API.
        // Defaults to any origin; override with CORS_ALLOWED_ORIGINS (comma-separated).
        const string CorsPolicy = "WebClient";
        var allowedOrigins = builder.Configuration["Cors:AllowedOrigins"]
            ?? Environment.GetEnvironmentVariable("CORS_ALLOWED_ORIGINS");
        builder.Services.AddCors(options =>
        {
            options.AddPolicy(CorsPolicy, policy =>
            {
                if (string.IsNullOrWhiteSpace(allowedOrigins) || allowedOrigins.Trim() == "*")
                {
                    policy.AllowAnyOrigin();
                }
                else
                {
                    policy.WithOrigins(allowedOrigins.Split(
                        ',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                }
                policy.AllowAnyHeader().AllowAnyMethod();
            });
        });

        // Managed hosts (Render/Railway/Heroku) inject the listening port via PORT.
        var port = Environment.GetEnvironmentVariable("PORT");
        if (!string.IsNullOrWhiteSpace(port))
        {
            builder.WebHost.UseUrls($"http://+:{port}");
        }

        // Connection string resolution order:
        //   1) ConnectionStrings:Default (appsettings / ConnectionStrings__Default env var)
        //   2) DATABASE_URL in postgres:// URL form (managed Postgres add-ons)
        //   3) local development default
        var conn = builder.Configuration.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(conn))
        {
            var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
            if (!string.IsNullOrWhiteSpace(databaseUrl))
            {
                conn = BuildNpgsqlConnectionString(databaseUrl);
            }
        }
        conn ??= "Host=localhost;Database=concurrent_booking;Username=postgres;Password=postgres";

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

        //if (app.Environment.IsDevelopment())
        //{
            app.UseSwagger();
            app.UseSwaggerUI();
        //}
        //if(!app.Environment.IsDevelopment())
        //{
        //    app.UseHttpsRedirection();
        //}
        
        // Serve the static web client (Api/wwwroot/index.html) at the site root.
        app.UseDefaultFiles();
        app.UseStaticFiles();

        app.UseCors(CorsPolicy);
        app.UseAuthorization();
        app.UseMiddleware<Api.Middleware.IdempotencyMiddleware>();
        app.MapControllers();
        await app.RunAsync();
    }

    // Converts a postgres://user:pass@host:port/db URL (Render/Railway/Heroku style)
    // into the Npgsql keyword connection string the app expects.
    private static string BuildNpgsqlConnectionString(string databaseUrl)
    {
        var uri = new Uri(databaseUrl);
        var userInfo = uri.UserInfo.Split(':', 2);
        var csb = new Npgsql.NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port <= 0 ? 5432 : uri.Port,
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty,
            Database = uri.AbsolutePath.TrimStart('/'),
            SslMode = Npgsql.SslMode.Prefer
        };
        return csb.ConnectionString;
    }
}
