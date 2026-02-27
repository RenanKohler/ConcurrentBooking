using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ConcurrentBooking.Application.Repositories;
using ConcurrentBooking.Domain.Entities;
using ConcurrentBooking.Infrastructure.Data;

namespace ConcurrentBooking.Infrastructure.Repositories;

public class EfSlotRepository : ISlotRepository
{
    private readonly BookingDbContext _db;

    public EfSlotRepository(BookingDbContext db)
    {
        _db = db;
    }

    public async Task<Slot?> GetByIdAsync(Guid id)
    {
        return await _db.Slots.FirstOrDefaultAsync(s => s.Id == id);
    }
}
