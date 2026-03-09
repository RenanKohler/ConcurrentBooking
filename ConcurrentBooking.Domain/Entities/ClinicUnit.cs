namespace ConcurrentBooking.Domain.Entities;

using System;

public sealed class ClinicUnit
{
    public Guid Id { get; init; }

    public string Name { get; init; }

    public ClinicUnit(string name)
    {
        Id = Guid.NewGuid();
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }
}
