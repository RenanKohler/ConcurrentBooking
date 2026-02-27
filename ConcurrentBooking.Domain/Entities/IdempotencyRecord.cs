namespace ConcurrentBooking.Domain.Entities;

using System;

public class IdempotencyRecord
{
    public Guid Id { get; init; }

    public string Key { get; init; }

    public string Route { get; init; }

    public string? RequestHash { get; init; }

    public string? ResponseBody { get; set; }

    public int? StatusCode { get; set; }

    public DateTime CreatedAt { get; init; }

    public IdempotencyRecord(string key, string route, string? requestHash = null)
    {
        Id = Guid.NewGuid();
        Key = key ?? throw new ArgumentNullException(nameof(key));
        Route = route ?? throw new ArgumentNullException(nameof(route));
        RequestHash = requestHash;
        CreatedAt = DateTime.UtcNow;
    }
}
