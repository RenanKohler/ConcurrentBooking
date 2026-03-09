namespace ConcurrentBooking.Domain.Entities;

using System;

public sealed class Specialty
{
    public Guid Id { get; init; }

    public string Name { get; init; }

    public Specialty(string name)
    {
        Id = Guid.NewGuid();
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }
}
