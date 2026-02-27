using ConcurrentBooking.Domain.Entities;
using System.Threading.Tasks;

namespace ConcurrentBooking.Application.Repositories
{
    public interface IIdempotencyRepository
    {
        Task<IdempotencyRecord?> GetByKeyAsync(string key, string route);

        Task AddAsync(IdempotencyRecord record);
    }
}
