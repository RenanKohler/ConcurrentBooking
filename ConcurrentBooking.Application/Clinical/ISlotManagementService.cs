namespace ConcurrentBooking.Application.Clinical;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

// CRUD over slots (horários). Implementations validate referenced entities and
// surface conflicts (duplicate slot, deleting a booked slot) as exceptions.
public interface ISlotManagementService
{
    Task<IReadOnlyCollection<SlotAdminItem>> ListAsync(SlotQuery query, CancellationToken cancellationToken);

    Task<SlotAdminItem?> GetAsync(Guid slotId, CancellationToken cancellationToken);

    Task<SlotAdminItem> CreateAsync(CreateSlotInput input, CancellationToken cancellationToken);

    Task<SlotAdminItem> UpdateAsync(Guid slotId, UpdateSlotInput input, CancellationToken cancellationToken);

    Task DeleteAsync(Guid slotId, CancellationToken cancellationToken);
}
