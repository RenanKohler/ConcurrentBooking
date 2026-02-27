using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using ConcurrentBooking.Application.Repositories;
using ConcurrentBooking.Domain.Entities;

namespace ConcurrentBooking.Infrastructure.Repositories
{
    public class InMemoryBookingRepository : IBookingRepository
    {
        private readonly ConcurrentDictionary<Guid, Booking> _store = new();

        public Task AddAsync(Booking booking)
        {
            // Enforce slot uniqueness to emulate DB unique constraint
            var exists = _store.Values.Any(b => b.SlotId == booking.SlotId && b.Status == BookingStatus.Confirmed);
            if (exists)
            {
                throw new InvalidOperationException("Unique constraint violation: slot already booked");
            }

            _store[booking.Id] = booking;
            return Task.CompletedTask;
        }

        public Task<Booking?> GetByIdAsync(Guid id)
        {
            _store.TryGetValue(id, out var b);
            return Task.FromResult(b);
        }

        public Task<bool> ExistsBookingForSlotAsync(Guid slotId)
        {
            var exists = _store.Values.Any(b => b.SlotId == slotId && b.Status == BookingStatus.Confirmed);
            return Task.FromResult(exists);
        }
    }
}
