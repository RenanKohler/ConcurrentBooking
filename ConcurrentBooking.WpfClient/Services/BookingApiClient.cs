using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;

namespace ConcurrentBooking.WpfClient.Services;

public sealed class BookingApiClient(HttpClient httpClient)
{
    public async Task<ApiCallResult<List<SpecialtyListItem>>> GetSpecialtiesAsync(CancellationToken cancellationToken)
    {
        var response = await httpClient.GetAsync("api/specialties", cancellationToken);
        return await ApiCallResult<List<SpecialtyListItem>>.FromResponseAsync(response, cancellationToken);
    }

    public async Task<ApiCallResult<List<ClinicUnitListItem>>> GetUnitsAsync(CancellationToken cancellationToken)
    {
        var response = await httpClient.GetAsync("api/units", cancellationToken);
        return await ApiCallResult<List<ClinicUnitListItem>>.FromResponseAsync(response, cancellationToken);
    }

    public async Task<ApiCallResult<List<ProfessionalSearchItem>>> GetProfessionalsAsync(
        Guid specialtyId,
        DateOnly date,
        Guid? unitId,
        CancellationToken cancellationToken)
    {
        var query = $"api/professionals?specialtyId={specialtyId}&date={date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}";
        if (unitId.HasValue)
        {
            query += $"&unitId={unitId.Value}";
        }

        var response = await httpClient.GetAsync(query, cancellationToken);
        return await ApiCallResult<List<ProfessionalSearchItem>>.FromResponseAsync(response, cancellationToken);
    }

    public async Task<ApiCallResult<List<AvailableSlotItem>>> GetAvailabilityAsync(
        Guid professionalId,
        DateOnly date,
        AvailabilityPeriod period,
        Guid unitId,
        CancellationToken cancellationToken)
    {
        var query =
            $"api/professionals/{professionalId}/availability?date={date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}&period={period}&unitId={unitId}";

        var response = await httpClient.GetAsync(query, cancellationToken);
        return await ApiCallResult<List<AvailableSlotItem>>.FromResponseAsync(response, cancellationToken);
    }

    public async Task<ApiCallResult<HoldResult>> CreateHoldAsync(Guid slotId, Guid customerId, string idempotencyKey, CancellationToken cancellationToken)
    {
        var response = await httpClient.PostAsJsonAsync($"api/slots/{slotId}/hold", new CreateHoldRequest(customerId, idempotencyKey), cancellationToken);
        return await ApiCallResult<HoldResult>.FromResponseAsync(response, cancellationToken);
    }

    public async Task<ApiCallResult<ConfirmResult>> ConfirmHoldAsync(Guid holdId, Guid customerId, string idempotencyKey, CancellationToken cancellationToken)
    {
        var response = await httpClient.PostAsJsonAsync($"api/holds/{holdId}/confirm", new ConfirmRequest(customerId, idempotencyKey), cancellationToken);
        return await ApiCallResult<ConfirmResult>.FromResponseAsync(response, cancellationToken);
    }

    private sealed record CreateHoldRequest(Guid CustomerId, string IdempotencyKey);
    private sealed record ConfirmRequest(Guid CustomerId, string IdempotencyKey);
}

public enum AvailabilityPeriod
{
    Morning,
    Afternoon,
    Evening
}

public sealed record SpecialtyListItem(Guid Id, string Name);
public sealed record ClinicUnitListItem(Guid Id, string Name);

public sealed record ProfessionalSearchItem(
    Guid ProfessionalId,
    string ProfessionalName,
    Guid SpecialtyId,
    string SpecialtyName,
    Guid ClinicUnitId,
    string ClinicUnitName);

public sealed record AvailableSlotItem(Guid SlotId, DateTime StartsAt, DateTime EndsAt, string? SeatCode);
public sealed record HoldResult(Guid HoldId, DateTime ExpiresAt);
public sealed record ConfirmResult(Guid BookingId);

public sealed record ApiError(string Error);

public sealed class ApiCallResult<T>(bool isSuccess, T? data, string? error, HttpStatusCode statusCode)
{
    public bool IsSuccess { get; } = isSuccess;
    public T? Data { get; } = data;
    public string? Error { get; } = error;
    public HttpStatusCode StatusCode { get; } = statusCode;

    public static async Task<ApiCallResult<T>> FromResponseAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken);
            return new ApiCallResult<T>(true, body, null, response.StatusCode);
        }

        var apiError = await response.Content.ReadFromJsonAsync<ApiError>(cancellationToken: cancellationToken);
        var errorMessage = apiError?.Error ?? $"Falha HTTP {(int)response.StatusCode} ({response.StatusCode}).";
        return new ApiCallResult<T>(false, default, errorMessage, response.StatusCode);
    }
}
