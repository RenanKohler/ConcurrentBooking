using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ConcurrentBooking.Application.Repositories;
using ConcurrentBooking.Domain.Entities;
using ConcurrentBooking.Infrastructure.Data;

namespace ConcurrentBooking.Infrastructure.Repositories;

public class EfBookingRepository : IBookingRepository
{
    private readonly BookingDbContext _db;

    public EfBookingRepository(BookingDbContext db)
    {
        _db = db;
    }

    public async Task AddAsync(Booking booking)
    {
        await _db.Bookings.AddAsync(booking);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            // translate unique constraint violation to domain error
            throw new InvalidOperationException("Unique constraint violation: slot already booked", ex);
        }
    }

    public async Task<Booking?> GetByIdAsync(Guid id)
    {
        return await _db.Bookings.FirstOrDefaultAsync(b => b.Id == id);
    }

    public async Task<bool> ExistsBookingForSlotAsync(Guid slotId)
    {
        return await _db.Bookings.AnyAsync(b => b.SlotId == slotId && b.Status == BookingStatus.Confirmed);
    }
}
