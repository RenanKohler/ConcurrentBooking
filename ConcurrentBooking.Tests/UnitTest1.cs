using System;
using System.Linq;
using System.Threading.Tasks;
using ConcurrentBooking.Infrastructure.Repositories;
using ConcurrentBooking.Application.UseCases;
using ConcurrentBooking.Application.Dtos;
using Xunit;

namespace ConcurrentBooking.Tests
{
    public class ConcurrencyTests
    {
        [Fact]
        public async Task OnlyOneConfirmCreatesBookingUnderConcurrentConfirms()
        {
            // Arrange
            var slotRepo = new InMemorySlotRepository();
            var holdRepo = new InMemoryHoldRepository();
            var bookingRepo = new InMemoryBookingRepository();

            var holdHandler = new HoldSlotHandler(slotRepo, holdRepo, bookingRepo);
            var confirmHandler = new ConfirmBookingHandler(holdRepo, bookingRepo);

            var slotId = slotRepo.SeededSlotId;
            var customerId = Guid.NewGuid();

            // Create a hold first
            var holdResult = await holdHandler.Handle(new HoldSlotRequest(slotId, customerId, "init-hold-key"));

            var tasks = Enumerable.Range(0, 20).Select(i => Task.Run(async () =>
            {
                try
                {
                    var res = await confirmHandler.Handle(new ConfirmBookingRequest(holdResult.HoldId, customerId, $"confirm-key-{i}"));
                    return (success: true, id: res.BookingId);
                }
                catch (InvalidOperationException)
                {
                    return (success: false, id: Guid.Empty);
                }
            })).ToArray();

            // Act
            var results = await Task.WhenAll(tasks);

            // Assert
            var successCount = results.Count(r => r.success);
            Assert.Equal(1, successCount);
            Assert.True(await bookingRepo.ExistsBookingForSlotAsync(slotId));
        }
    }
}
