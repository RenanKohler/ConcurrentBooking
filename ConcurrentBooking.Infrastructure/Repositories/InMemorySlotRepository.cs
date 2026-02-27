using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using ConcurrentBooking.Application.Repositories;
using ConcurrentBooking.Domain.Entities;

namespace ConcurrentBooking.Infrastructure.Repositories
{
    public class InMemorySlotRepository : ISlotRepository
    {
        private readonly ConcurrentDictionary<Guid, Slot> _store = new();

        public Guid SeededSlotId { get; }

        public InMemorySlotRepository()
        {
            // for MVP create a sample slot
            var slot = new Slot { Id = Guid.NewGuid(), ResourceId = Guid.NewGuid(), StartsAt = DateTime.UtcNow, EndsAt = DateTime.UtcNow.AddHours(1), SeatCode = "A1" };
            _store[slot.Id] = slot;
            SeededSlotId = slot.Id;
        }

        public Task<Slot?> GetByIdAsync(Guid id)
        {
            _store.TryGetValue(id, out var s);
            return Task.FromResult(s);
        }
    }
}
