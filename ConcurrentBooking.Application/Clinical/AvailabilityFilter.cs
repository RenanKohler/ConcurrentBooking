namespace ConcurrentBooking.Application.Clinical;

using System;
using System.Collections.Generic;
using System.Linq;
using ConcurrentBooking.Domain.Entities;

public static class AvailabilityFilter
{
    public static IReadOnlyCollection<Slot> FilterAvailable(
        IEnumerable<Slot> candidateSlots,
        IEnumerable<Hold> holds,
        IEnumerable<Booking> bookings,
        DateTime nowUtc)
    {
        var activeHeldSlotIds = holds
            .Where(h => h.Status == HoldStatus.Active && h.ExpiresAt > nowUtc)
            .Select(h => h.SlotId)
            .ToHashSet();

        var bookedSlotIds = bookings
            .Where(b => b.Status == BookingStatus.Confirmed)
            .Select(b => b.SlotId)
            .ToHashSet();

        return candidateSlots
            .Where(slot => !activeHeldSlotIds.Contains(slot.Id) && !bookedSlotIds.Contains(slot.Id))
            .OrderBy(slot => slot.StartsAt)
            .ToArray();
    }
}
