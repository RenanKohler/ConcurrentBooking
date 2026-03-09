namespace Api.Services;

using System;
using System.Threading;
using System.Threading.Tasks;
using ConcurrentBooking.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

public sealed class MigratingDatabaseInitializer : IDatabaseInitializer
{
    private readonly BookingDbContext _db;
    private readonly IClinicalDemoDataSeeder _seeder;
    private readonly ILogger<MigratingDatabaseInitializer> _logger;

    public MigratingDatabaseInitializer(
        BookingDbContext db,
        IClinicalDemoDataSeeder seeder,
        ILogger<MigratingDatabaseInitializer> logger)
    {
        _db = db;
        _seeder = seeder;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Applying database migrations...");
            await _db.Database.MigrateAsync(cancellationToken);
            _logger.LogInformation("Database migrations applied.");

            await _seeder.SeedAsync(_db, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed during database startup.");
            throw;
        }
    }
}
