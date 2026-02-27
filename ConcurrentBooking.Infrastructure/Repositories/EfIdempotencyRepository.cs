using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ConcurrentBooking.Application.Repositories;
using ConcurrentBooking.Domain.Entities;
using ConcurrentBooking.Infrastructure.Data;

namespace ConcurrentBooking.Infrastructure.Repositories;

public class EfIdempotencyRepository : IIdempotencyRepository
{
    private readonly BookingDbContext _db;

    public EfIdempotencyRepository(BookingDbContext db)
    {
        _db = db;
    }

    public async Task AddAsync(IdempotencyRecord record)
    {
        await _db.IdempotencyRecords.AddAsync(record);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            // Ignore unique race; another request may have inserted same idempotency key
            throw new InvalidOperationException("Idempotency conflict", ex);
        }
    }

    public async Task<IdempotencyRecord?> GetByKeyAsync(string key, string route)
    {
        return await _db.IdempotencyRecords.FirstOrDefaultAsync(r => r.Key == key && r.Route == route);
    }
}
