using System.Data.Common;
using Api.Services;
using ConcurrentBooking.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ConcurrentBooking.Tests.Integration;

public sealed class ClinicalDiscoveryApiFactory : WebApplicationFactory<Api.Program>
{
    private SqliteConnection? _connection;

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<BookingDbContext>>();
            services.RemoveAll<BookingDbContext>();
            services.RemoveAll<IDatabaseInitializer>();

            services.AddSingleton<DbConnection>(_ =>
            {
                _connection ??= new SqliteConnection("Data Source=:memory:");
                if (_connection.State != System.Data.ConnectionState.Open)
                {
                    _connection.Open();
                }

                return _connection;
            });

            services.AddDbContext<BookingDbContext>((serviceProvider, options) =>
            {
                var connection = serviceProvider.GetRequiredService<DbConnection>();
                options.UseSqlite(connection);
            });

            services.AddScoped<IDatabaseInitializer, SqliteTestDatabaseInitializer>();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _connection?.Dispose();
    }

    private sealed class SqliteTestDatabaseInitializer : IDatabaseInitializer
    {
        private readonly BookingDbContext _db;
        private readonly IClinicalDemoDataSeeder _seeder;

        public SqliteTestDatabaseInitializer(BookingDbContext db, IClinicalDemoDataSeeder seeder)
        {
            _db = db;
            _seeder = seeder;
        }

        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            await _db.Database.EnsureDeletedAsync(cancellationToken);
            await _db.Database.EnsureCreatedAsync(cancellationToken);
            await _seeder.SeedAsync(_db, cancellationToken);
        }
    }
}
