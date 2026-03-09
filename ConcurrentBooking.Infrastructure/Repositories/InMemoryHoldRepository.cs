using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ConcurrentBooking.Application.Repositories;
using ConcurrentBooking.Domain.Entities;

namespace ConcurrentBooking.Infrastructure.Repositories
{
    public class InMemoryHoldRepository : IHoldRepository
    {
        private readonly ConcurrentDictionary<Guid, Hold> _store = new();

        public Task AddAsync(Hold hold)
        {
            _store[hold.Id] = hold;
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<Hold?> GetByIdAsync(Guid id)
        {
            _store.TryGetValue(id, out var h);
            return Task.FromResult(h);
        }

        public Task RemoveAsync(Guid id)
        {
            _store.TryRemove(id, out _);
            return Task.CompletedTask;
        }

        public Task<bool> ExistsActiveHoldForSlotAsync(Guid slotId)
        {
            var exists = _store.Values.Any(h => h.SlotId == slotId && h.Status == HoldStatus.Active && !h.IsExpired());
            return Task.FromResult(exists);
        }

        public Task<int> ExpireHoldsAsync(CancellationToken cancellationToken = default)
        {
            var expired = _store.Values
                .Where(h => h.Status == HoldStatus.Active && h.IsExpired())
                .ToList();

            foreach (var hold in expired)
                hold.MarkExpired();

            return Task.FromResult(expired.Count);
        }
    }
}
