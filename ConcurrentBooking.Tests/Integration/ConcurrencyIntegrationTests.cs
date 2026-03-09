using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ConcurrentBooking.Application.Dtos;
using ConcurrentBooking.Application.UseCases;
using ConcurrentBooking.Domain.Entities;
using ConcurrentBooking.Infrastructure.Repositories;
using Xunit;

namespace ConcurrentBooking.Tests.Integration;

[Collection(PostgresCollection.Name)]
public class ConcurrencyIntegrationTests
{
    private readonly PostgresFixture _fixture;

    public ConcurrencyIntegrationTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    // --- helpers ---

    private async Task<Guid> SeedSlotAsync()
    {
        await using var ctx = _fixture.CreateContext();
        var specialty = new Specialty("Test Specialty");
        var unit = new ClinicUnit("Test Unit");
        var professional = new Professional(specialty.Id, "Test Professional");
        var slot = new Slot
        {
            ProfessionalId = professional.Id,
            ClinicUnitId = unit.Id,
            StartsAt = DateTime.UtcNow.AddHours(1),
            EndsAt = DateTime.UtcNow.AddHours(2),
            SeatCode = Guid.NewGuid().ToString("N")[..6]
        };
        ctx.Specialties.Add(specialty);
        ctx.ClinicUnits.Add(unit);
        ctx.Professionals.Add(professional);
        ctx.Slots.Add(slot);
        await ctx.SaveChangesAsync();
        return slot.Id;
    }

    // --- tests ---

    [Fact]
    public async Task OnlyOneHoldSucceedsWhen100ConcurrentHoldRequestsRace()
    {
        var slotId = await SeedSlotAsync();
        var customerId = Guid.NewGuid();

        var tasks = Enumerable.Range(0, 100).Select(i => Task.Run(async () =>
        {
            // Each task gets its own DbContext + repositories (required for concurrency)
            var ctx = _fixture.CreateContext();
            var handler = new HoldSlotHandler(
                new EfSlotRepository(ctx),
                new EfHoldRepository(ctx),
                new EfBookingRepository(ctx));

            try
            {
                var result = await handler.Handle(
                    new HoldSlotRequest(slotId, customerId, $"hold-race-{i}-{Guid.NewGuid()}"));
                return (success: true, holdId: result.HoldId);
            }
            catch (InvalidOperationException)
            {
                return (success: false, holdId: Guid.Empty);
            }
            catch (DbUpdateException)
            {
                // DB unique constraint on active holds blocked the race
                return (success: false, holdId: Guid.Empty);
            }
            finally
            {
                await ctx.DisposeAsync();
            }
        })).ToArray();

        var results = await Task.WhenAll(tasks);

        var successCount = results.Count(r => r.success);
        Assert.Equal(1, successCount);

        // Verify exactly one active hold in DB
        await using var verifyCtx = _fixture.CreateContext();
        var activeHolds = await verifyCtx.Holds
            .Where(h => h.SlotId == slotId && h.Status == HoldStatus.Active)
            .CountAsync();
        Assert.Equal(1, activeHolds);
    }

    [Fact]
    public async Task OnlyOneBookingSucceedsWhen100ConcurrentConfirmRequestsRace()
    {
        var slotId = await SeedSlotAsync();
        var customerId = Guid.NewGuid();

        // Create a single hold using a dedicated context
        Guid holdId;
        {
            var ctx = _fixture.CreateContext();
            var handler = new HoldSlotHandler(
                new EfSlotRepository(ctx),
                new EfHoldRepository(ctx),
                new EfBookingRepository(ctx));

            var holdResult = await handler.Handle(
                new HoldSlotRequest(slotId, customerId, $"seed-hold-{Guid.NewGuid()}"));
            holdId = holdResult.HoldId;
            await ctx.DisposeAsync();
        }

        // Race 100 confirm attempts against the same hold
        var tasks = Enumerable.Range(0, 100).Select(i => Task.Run(async () =>
        {
            var ctx = _fixture.CreateContext();
            var handler = new ConfirmBookingHandler(
                new EfHoldRepository(ctx),
                new EfBookingRepository(ctx));

            try
            {
                var result = await handler.Handle(
                    new ConfirmBookingRequest(holdId, customerId, $"confirm-race-{i}-{Guid.NewGuid()}"));
                return (success: true, bookingId: result.BookingId);
            }
            catch (InvalidOperationException)
            {
                return (success: false, bookingId: Guid.Empty);
            }
            catch (DbUpdateException)
            {
                // DB UNIQUE constraint on bookings.SlotId blocked the duplicate
                return (success: false, bookingId: Guid.Empty);
            }
            finally
            {
                await ctx.DisposeAsync();
            }
        })).ToArray();

        var results = await Task.WhenAll(tasks);

        var successCount = results.Count(r => r.success);
        Assert.Equal(1, successCount);

        // Verify exactly one booking in DB
        await using var verifyCtx = _fixture.CreateContext();
        var bookingCount = await verifyCtx.Bookings
            .Where(b => b.SlotId == slotId)
            .CountAsync();
        Assert.Equal(1, bookingCount);
    }

    [Fact]
    public async Task ConfirmAfterHoldExpiredReturnsError()
    {
        var slotId = await SeedSlotAsync();
        var customerId = Guid.NewGuid();

        // Create hold with 1 ms TTL so it expires immediately
        Hold expiredHold;
        {
            await using var ctx = _fixture.CreateContext();
            expiredHold = new Hold(slotId, customerId, TimeSpan.FromMilliseconds(1), $"exp-hold-{Guid.NewGuid()}");
            ctx.Holds.Add(expiredHold);
            await ctx.SaveChangesAsync();
        }

        // Ensure hold is expired
        await Task.Delay(20);

        await using var confirmCtx = _fixture.CreateContext();
        var handler = new ConfirmBookingHandler(
            new EfHoldRepository(confirmCtx),
            new EfBookingRepository(confirmCtx));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new ConfirmBookingRequest(expiredHold.Id, customerId, $"confirm-exp-{Guid.NewGuid()}")));

        Assert.Contains("expired", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
