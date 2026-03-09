namespace ConcurrentBooking.Application.Clinical;

using System;

public readonly record struct AvailabilityWindow(DateTime StartsAt, DateTime EndsAt)
{
    public static AvailabilityWindow For(DateOnly date, AvailabilityPeriod period)
    {
        var dayStart = DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);

        return period switch
        {
            AvailabilityPeriod.Morning => new AvailabilityWindow(dayStart.AddHours(6), dayStart.AddHours(12)),
            AvailabilityPeriod.Afternoon => new AvailabilityWindow(dayStart.AddHours(12), dayStart.AddHours(18)),
            AvailabilityPeriod.Evening => new AvailabilityWindow(dayStart.AddHours(18), dayStart.AddHours(23)),
            _ => throw new ArgumentOutOfRangeException(nameof(period), period, "Unsupported availability period.")
        };
    }
}
