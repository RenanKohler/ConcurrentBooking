namespace ConcurrentBooking.Domain.Entities;

using System;

public class Resource
{
    public Guid Id { get; init; }

    public string Name { get; init; }

    public Resource(string name)
    {
        Id = Guid.NewGuid();
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }
}
