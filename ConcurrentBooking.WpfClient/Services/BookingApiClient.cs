using System.Net;
using System.Net.Http;
using System.Net.Http.Json;

namespace ConcurrentBooking.WpfClient.Services;

public sealed class BookingApiClient(HttpClient httpClient)
{
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

    public async Task<ApiCallResult<SlotDto>> CreateSlotAsync(string resourceName, DateTime startsAt, DateTime endsAt, string? seatCode, CancellationToken cancellationToken)
    {
        var response = await httpClient.PostAsJsonAsync(
            "api/slots",
            new CreateSlotRequest(resourceName, startsAt, endsAt, seatCode),
            cancellationToken);

        return await ApiCallResult<SlotDto>.FromResponseAsync(response, cancellationToken);
    }

    public async Task<ApiCallResult<List<SlotDto>>> GetSlotsAsync(CancellationToken cancellationToken)
    {
        var response = await httpClient.GetAsync("api/slots", cancellationToken);
        return await ApiCallResult<List<SlotDto>>.FromResponseAsync(response, cancellationToken);
    }

    private sealed record CreateHoldRequest(Guid CustomerId, string IdempotencyKey);
    private sealed record ConfirmRequest(Guid CustomerId, string IdempotencyKey);
    private sealed record CreateSlotRequest(string ResourceName, DateTime StartsAt, DateTime EndsAt, string? SeatCode);
}

public sealed record HoldResult(Guid HoldId, DateTime ExpiresAt);
public sealed record ConfirmResult(Guid BookingId);
public sealed record SlotDto(Guid Id, Guid ResourceId, string ResourceName, DateTime StartsAt, DateTime EndsAt, string? SeatCode);

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
