using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ConcurrentBooking.Application.Repositories;
using ConcurrentBooking.Domain.Entities;
using ConcurrentBooking.Infrastructure.Data;

namespace ConcurrentBooking.Infrastructure.Repositories;

public class EfHoldRepository : IHoldRepository
{
    private readonly BookingDbContext _db;

    public EfHoldRepository(BookingDbContext db)
    {
        _db = db;
    }

    public async Task AddAsync(Hold hold)
    {
        await _db.Holds.AddAsync(hold);
        await _db.SaveChangesAsync();
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<Hold?> GetByIdAsync(Guid id)
    {
        return await _db.Holds.FirstOrDefaultAsync(h => h.Id == id);
    }

    public async Task RemoveAsync(Guid id)
    {
        var ent = await _db.Holds.FindAsync(id);
        if (ent != null)
        {
            _db.Holds.Remove(ent);
            await _db.SaveChangesAsync();
        }
    }

    public async Task<bool> ExistsActiveHoldForSlotAsync(Guid slotId)
    {
        return await _db.Holds.AnyAsync(h => h.SlotId == slotId && h.Status == HoldStatus.Active && h.ExpiresAt > DateTime.UtcNow);
    }

    public async Task<int> ExpireHoldsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var expiredHolds = await _db.Holds
            .Where(h => h.Status == HoldStatus.Active && h.ExpiresAt <= now)
            .ToListAsync(cancellationToken);

        foreach (var hold in expiredHolds)
            hold.MarkExpired();

        if (expiredHolds.Count > 0)
            await _db.SaveChangesAsync(cancellationToken);

        return expiredHolds.Count;
    }
}
