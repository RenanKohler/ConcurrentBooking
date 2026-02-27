using System;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using ConcurrentBooking.Infrastructure.Data;

namespace ConcurrentBooking.Tests.Integration;

/// <summary>
/// Spins up a real PostgreSQL container once per test collection and exposes
/// a factory that creates a new <see cref="BookingDbContext"/> for each test.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:15")
        .WithDatabase("booking_test")
        .WithUsername("test")
        .WithPassword("test")
        .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5432))
        .Build();

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();

        // Apply schema using EF Core migrations (runs all pending migrations including
        // the partial unique index on holds and the UNIQUE constraint on bookings.SlotId)
        await using var ctx = CreateContext();
        await ctx.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    /// <summary>Creates a fresh <see cref="BookingDbContext"/> bound to the test database.</summary>
    public BookingDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<BookingDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        return new BookingDbContext(options);
    }
}

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "Postgres";
}
