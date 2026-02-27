using ConcurrentBooking.Application.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Api.Services;

/// <summary>
/// Periodically scans for Active holds past their expiry time and transitions them to Expired,
/// releasing the slot for new reservations.
/// </summary>
public sealed class HoldExpirationService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<HoldExpirationService> _logger;

    public HoldExpirationService(IServiceScopeFactory scopeFactory, ILogger<HoldExpirationService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("HoldExpirationService started. Interval: {Interval}s", Interval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await ExpireHoldsAsync(stoppingToken);
        }

        _logger.LogInformation("HoldExpirationService stopped.");
    }

    private async Task ExpireHoldsAsync(CancellationToken cancellationToken)
    {
        try
        {
            // IHoldRepository is Scoped, so we need a new scope per tick
            await using var scope = _scopeFactory.CreateAsyncScope();
            var holdRepo = scope.ServiceProvider.GetRequiredService<IHoldRepository>();

            var count = await holdRepo.ExpireHoldsAsync(cancellationToken);
            if (count > 0)
                _logger.LogInformation("Expired {Count} holds.", count);
        }
        catch (OperationCanceledException)
        {
            // Graceful shutdown; no action needed
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while expiring holds.");
        }
    }
}
