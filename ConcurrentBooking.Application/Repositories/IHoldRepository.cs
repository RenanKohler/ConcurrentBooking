using ConcurrentBooking.Domain.Entities;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ConcurrentBooking.Application.Repositories
{
    public interface IHoldRepository
    {
        Task<Hold?> GetByIdAsync(Guid id);

        Task AddAsync(Hold hold);

        Task RemoveAsync(Guid id);

        Task<bool> ExistsActiveHoldForSlotAsync(Guid slotId);

        /// <summary>Transitions all Active holds past their expiry time to Expired status.</summary>
        /// <returns>Number of holds expired.</returns>
        Task<int> ExpireHoldsAsync(CancellationToken cancellationToken = default);
    }
}
