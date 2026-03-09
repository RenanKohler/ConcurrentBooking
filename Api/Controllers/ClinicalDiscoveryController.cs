using ConcurrentBooking.Application.Clinical;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api")]
public sealed class ClinicalDiscoveryController : ControllerBase
{
    private readonly IClinicalDiscoveryService _discoveryService;

    public ClinicalDiscoveryController(IClinicalDiscoveryService discoveryService)
    {
        _discoveryService = discoveryService;
    }

    [HttpGet("specialties")]
    public async Task<ActionResult<IReadOnlyCollection<SpecialtyListItem>>> GetSpecialties(CancellationToken cancellationToken)
    {
        var result = await _discoveryService.GetSpecialtiesAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("units")]
    public async Task<ActionResult<IReadOnlyCollection<ClinicUnitListItem>>> GetUnits(CancellationToken cancellationToken)
    {
        var result = await _discoveryService.GetUnitsAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("professionals")]
    public async Task<ActionResult<IReadOnlyCollection<ProfessionalSearchItem>>> GetProfessionals(
        [FromQuery] Guid specialtyId,
        [FromQuery] DateOnly date,
        [FromQuery] Guid? unitId,
        CancellationToken cancellationToken)
    {
        var result = await _discoveryService.GetProfessionalsAsync(
            new ProfessionalSearchQuery(specialtyId, date, unitId),
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("professionals/{professionalId:guid}/availability")]
    public async Task<ActionResult<IReadOnlyCollection<AvailableSlotItem>>> GetAvailability(
        [FromRoute] Guid professionalId,
        [FromQuery] DateOnly date,
        [FromQuery] AvailabilityPeriod period,
        [FromQuery] Guid unitId,
        CancellationToken cancellationToken)
    {
        var result = await _discoveryService.GetAvailabilityAsync(
            new ProfessionalAvailabilityQuery(professionalId, date, period, unitId),
            cancellationToken);

        return Ok(result);
    }
}
