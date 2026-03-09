using System.Linq;
using ConcurrentBooking.Application.Dtos;
using ConcurrentBooking.Application.UseCases;
using ConcurrentBooking.Infrastructure.Repositories;

namespace ConcurrentBooking.Tests;

public sealed class ConcurrencyTests
{
    [Fact]
    public async Task OnlyOneConfirmCreatesBookingUnderConcurrentConfirms()
    {
        var slotRepo = new InMemorySlotRepository();
        var holdRepo = new InMemoryHoldRepository();
        var bookingRepo = new InMemoryBookingRepository();

        var holdHandler = new HoldSlotHandler(slotRepo, holdRepo, bookingRepo);
        var confirmHandler = new ConfirmBookingHandler(holdRepo, bookingRepo);

        var slotId = slotRepo.SeededSlotId;
        var customerId = Guid.NewGuid();

        var holdResult = await holdHandler.Handle(new HoldSlotRequest(slotId, customerId, "init-hold-key"));

        var tasks = Enumerable.Range(0, 20).Select(i => Task.Run(async () =>
        {
            try
            {
                var result = await confirmHandler.Handle(
                    new ConfirmBookingRequest(holdResult.HoldId, customerId, $"confirm-key-{i}"));
                return (success: true, bookingId: result.BookingId);
            }
            catch (InvalidOperationException)
            {
                return (success: false, bookingId: Guid.Empty);
            }
        })).ToArray();

        var results = await Task.WhenAll(tasks);

        Assert.Equal(1, results.Count(result => result.success));
        Assert.True(await bookingRepo.ExistsBookingForSlotAsync(slotId));
    }
}
