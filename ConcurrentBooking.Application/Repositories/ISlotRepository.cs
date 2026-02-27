using ConcurrentBooking.Domain.Entities;
using System;
using System.Threading.Tasks;

namespace ConcurrentBooking.Application.Repositories
{
    public interface ISlotRepository
    {
        Task<Slot?> GetByIdAsync(Guid id);
    }
}
