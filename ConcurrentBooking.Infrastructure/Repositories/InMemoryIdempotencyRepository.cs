using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using ConcurrentBooking.Application.Repositories;
using ConcurrentBooking.Domain.Entities;

namespace ConcurrentBooking.Infrastructure.Repositories
{
    public class InMemoryIdempotencyRepository : IIdempotencyRepository
    {
        private readonly ConcurrentDictionary<string, IdempotencyRecord> _store = new();

        private static string Key(string key, string route) => $"{route}:{key}";

        public Task AddAsync(IdempotencyRecord record)
        {
            var k = Key(record.Key, record.Route);
            _store[k] = record;
            return Task.CompletedTask;
        }

        public Task<IdempotencyRecord?> GetByKeyAsync(string key, string route)
        {
            _store.TryGetValue(Key(key, route), out var r);
            return Task.FromResult(r);
        }
    }
}
