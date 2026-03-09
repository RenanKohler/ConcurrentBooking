using ConcurrentBooking.Application.Clinical;
using ConcurrentBooking.Domain.Entities;

namespace ConcurrentBooking.Tests;

public sealed class AvailabilityFilterTests
{
    [Fact]
    public void MorningWindow_MapsExpectedBounds()
    {
        var date = new DateOnly(2026, 3, 10);

        var window = AvailabilityWindow.For(date, AvailabilityPeriod.Morning);

        Assert.Equal(new DateTime(2026, 3, 10, 6, 0, 0, DateTimeKind.Utc), window.StartsAt);
        Assert.Equal(new DateTime(2026, 3, 10, 12, 0, 0, DateTimeKind.Utc), window.EndsAt);
    }

    [Fact]
    public void FilterAvailable_ExcludesBookedSlots()
    {
        var slot = CreateSlot();
        var booking = new Booking(slot.Id, Guid.NewGuid(), Guid.NewGuid(), "booking-key");

        var result = AvailabilityFilter.FilterAvailable(new[] { slot }, Array.Empty<Hold>(), new[] { booking }, DateTime.UtcNow);

        Assert.Empty(result);
    }

    [Fact]
    public void FilterAvailable_ExcludesActiveHeldSlots()
    {
        var slot = CreateSlot();
        var hold = new Hold(slot.Id, Guid.NewGuid(), TimeSpan.FromMinutes(5), "hold-key");

        var result = AvailabilityFilter.FilterAvailable(new[] { slot }, new[] { hold }, Array.Empty<Booking>(), DateTime.UtcNow);

        Assert.Empty(result);
    }

    [Fact]
    public void FilterAvailable_KeepsSlotsWithExpiredHold()
    {
        var slot = CreateSlot();
        var expiredHold = new Hold(slot.Id, Guid.NewGuid(), TimeSpan.FromMinutes(-1), "expired-hold");

        var result = AvailabilityFilter.FilterAvailable(new[] { slot }, new[] { expiredHold }, Array.Empty<Booking>(), DateTime.UtcNow);

        Assert.Single(result);
        Assert.Equal(slot.Id, result.Single().Id);
    }

    private static Slot CreateSlot()
    {
        return new Slot
        {
            Id = Guid.NewGuid(),
            ProfessionalId = Guid.NewGuid(),
            ClinicUnitId = Guid.NewGuid(),
            StartsAt = DateTime.UtcNow.AddHours(1),
            EndsAt = DateTime.UtcNow.AddHours(2),
            SeatCode = "A1"
        };
    }
}
