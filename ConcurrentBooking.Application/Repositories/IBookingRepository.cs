using ConcurrentBooking.Domain.Entities;
using System;
using System.Threading.Tasks;

namespace ConcurrentBooking.Application.Repositories
{
    public interface IBookingRepository
    {
        Task<Booking?> GetByIdAsync(Guid id);

        Task AddAsync(Booking booking);

        Task<bool> ExistsBookingForSlotAsync(Guid slotId);
    }
}
