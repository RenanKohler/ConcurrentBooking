using System.Net;
using System.Net.Http.Json;
using ConcurrentBooking.Application.Clinical;

namespace ConcurrentBooking.Tests.Integration;

public sealed class ClinicalDiscoveryApiTests : IClassFixture<ClinicalDiscoveryApiFactory>
{
    private readonly HttpClient _client;

    public ClinicalDiscoveryApiTests(ClinicalDiscoveryApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetSpecialties_ReturnsSeededSpecialties()
    {
        var response = await _client.GetAsync("/api/specialties");

        response.EnsureSuccessStatusCode();

        var specialties = await response.Content.ReadFromJsonAsync<List<SpecialtyListItem>>();
        Assert.NotNull(specialties);
        Assert.Contains(specialties!, specialty => specialty.Name == "Cardiologia");
    }

    [Fact]
    public async Task GetUnits_ReturnsSeededUnits()
    {
        var response = await _client.GetAsync("/api/units");

        response.EnsureSuccessStatusCode();

        var units = await response.Content.ReadFromJsonAsync<List<ClinicUnitListItem>>();
        Assert.NotNull(units);
        Assert.Contains(units!, unit => unit.Name == "Unidade Centro");
    }

    [Fact]
    public async Task GetProfessionals_FlattensProfessionalUnitRows()
    {
        var specialty = await GetSpecialtyByNameAsync("Cardiologia");
        var date = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(1));

        var professionals = await _client.GetFromJsonAsync<List<ProfessionalSearchItem>>(
            $"/api/professionals?specialtyId={specialty.Id}&date={date:yyyy-MM-dd}");

        Assert.NotNull(professionals);
        Assert.Equal(2, professionals!.Count);
        Assert.Single(professionals.Select(professional => professional.ProfessionalId).Distinct());
        Assert.Equal(2, professionals.Select(professional => professional.ClinicUnitId).Distinct().Count());
    }

    [Fact]
    public async Task Availability_HidesSlotAfterHoldAndAfterBooking()
    {
        var specialty = await GetSpecialtyByNameAsync("Cardiologia");
        var date = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(1));
        var professional = (await _client.GetFromJsonAsync<List<ProfessionalSearchItem>>(
            $"/api/professionals?specialtyId={specialty.Id}&date={date:yyyy-MM-dd}"))!.First();

        var initialAvailability = await _client.GetFromJsonAsync<List<AvailableSlotItem>>(
            $"/api/professionals/{professional.ProfessionalId}/availability?date={date:yyyy-MM-dd}&period=Morning&unitId={professional.ClinicUnitId}");

        Assert.NotNull(initialAvailability);
        Assert.NotEmpty(initialAvailability!);
        var selectedSlot = initialAvailability![0];

        var holdResponse = await _client.PostAsJsonAsync(
            $"/api/slots/{selectedSlot.SlotId}/hold",
            new { customerId = Guid.NewGuid(), idempotencyKey = Guid.NewGuid().ToString("N") });

        Assert.Equal(HttpStatusCode.Created, holdResponse.StatusCode);
        var hold = await holdResponse.Content.ReadFromJsonAsync<HoldResponse>();
        Assert.NotNull(hold);

        var afterHold = await _client.GetFromJsonAsync<List<AvailableSlotItem>>(
            $"/api/professionals/{professional.ProfessionalId}/availability?date={date:yyyy-MM-dd}&period=Morning&unitId={professional.ClinicUnitId}");

        Assert.NotNull(afterHold);
        Assert.DoesNotContain(afterHold!, slot => slot.SlotId == selectedSlot.SlotId);

        var confirmResponse = await _client.PostAsJsonAsync(
            $"/api/holds/{hold!.HoldId}/confirm",
            new { customerId = Guid.NewGuid(), idempotencyKey = Guid.NewGuid().ToString("N") });

        Assert.Equal(HttpStatusCode.Created, confirmResponse.StatusCode);

        var afterBooking = await _client.GetFromJsonAsync<List<AvailableSlotItem>>(
            $"/api/professionals/{professional.ProfessionalId}/availability?date={date:yyyy-MM-dd}&period=Morning&unitId={professional.ClinicUnitId}");

        Assert.NotNull(afterBooking);
        Assert.DoesNotContain(afterBooking!, slot => slot.SlotId == selectedSlot.SlotId);
    }

    private async Task<SpecialtyListItem> GetSpecialtyByNameAsync(string name)
    {
        var specialties = await _client.GetFromJsonAsync<List<SpecialtyListItem>>("/api/specialties");
        return Assert.Single(specialties!, specialty => specialty.Name == name);
    }

    private sealed record HoldResponse(Guid HoldId, DateTime ExpiresAt);
}
