namespace ConcurrentBooking.Application.Clinical;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public interface IClinicalDiscoveryService
{
    Task<IReadOnlyCollection<SpecialtyListItem>> GetSpecialtiesAsync(CancellationToken cancellationToken);

    Task<IReadOnlyCollection<ClinicUnitListItem>> GetUnitsAsync(CancellationToken cancellationToken);

    // All professionals regardless of existing slots (used by the slot CRUD).
    Task<IReadOnlyCollection<ProfessionalListItem>> GetAllProfessionalsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyCollection<ProfessionalSearchItem>> GetProfessionalsAsync(
        ProfessionalSearchQuery query,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<AvailableSlotItem>> GetAvailabilityAsync(
        ProfessionalAvailabilityQuery query,
        CancellationToken cancellationToken);
}
