namespace ConcurrentBooking.Domain.Entities;

using System;

public sealed class Professional
{
    public Guid Id { get; init; }

    public Guid SpecialtyId { get; init; }

    public string FullName { get; init; }

    public Professional(Guid specialtyId, string fullName)
    {
        Id = Guid.NewGuid();
        SpecialtyId = specialtyId;
        FullName = fullName ?? throw new ArgumentNullException(nameof(fullName));
    }
}
