using Microsoft.EntityFrameworkCore;
using ConcurrentBooking.Domain.Entities;

namespace ConcurrentBooking.Infrastructure.Data;

public class BookingDbContext : DbContext
{
    public BookingDbContext(DbContextOptions<BookingDbContext> options) : base(options)
    {
    }

    public DbSet<Resource> Resources { get; set; } = null!;
    public DbSet<Slot> Slots { get; set; } = null!;
    public DbSet<Hold> Holds { get; set; } = null!;
    public DbSet<Booking> Bookings { get; set; } = null!;
    public DbSet<IdempotencyRecord> IdempotencyRecords { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Resource>(b =>
        {
            b.ToTable("resources");
            b.HasKey(x => x.Id);
            b.Property(x => x.Name).IsRequired();
        });

        modelBuilder.Entity<Slot>(b =>
        {
            b.ToTable("slots");
            b.HasKey(x => x.Id);
            b.Property(x => x.ResourceId).IsRequired();
            b.Property(x => x.StartsAt).IsRequired();
            b.Property(x => x.EndsAt).IsRequired();
            b.Property(x => x.SeatCode).HasMaxLength(50);
        });

        modelBuilder.Entity<Hold>(b =>
        {
            b.ToTable("holds");
            b.HasKey(x => x.Id);
            b.Property(x => x.SlotId).IsRequired();
            b.Property(x => x.CustomerId).IsRequired();
            b.Property(x => x.ExpiresAt).IsRequired();
            b.Property(x => x.Status).IsRequired();
            b.Property(x => x.CreatedAt).IsRequired();
            b.Property(x => x.RequestId).HasMaxLength(200);

            // unique request id for idempotency at DB level
            b.HasIndex("RequestId").IsUnique();

            // partial unique index to ensure at most one active hold per slot (Postgres filter)
            b.HasIndex("SlotId").HasDatabaseName("idx_holds_slot_active").HasFilter("\"Status\" = 'Active'").IsUnique();
        });

        modelBuilder.Entity<Booking>(b =>
        {
            b.ToTable("bookings");
            b.HasKey(x => x.Id);
            b.Property(x => x.SlotId).IsRequired();
            b.Property(x => x.CustomerId).IsRequired();
            b.Property(x => x.HoldId).IsRequired();
            b.Property(x => x.Status).IsRequired();
            b.Property(x => x.CreatedAt).IsRequired();
            b.Property(x => x.RequestId).HasMaxLength(200);

            b.HasIndex(x => x.SlotId).IsUnique();
            b.HasIndex("RequestId").IsUnique();
        });

        modelBuilder.Entity<IdempotencyRecord>(b =>
        {
            b.ToTable("idempotency");
            b.HasKey(x => x.Id);
            b.Property(x => x.Key).IsRequired();
            b.Property(x => x.Route).IsRequired();
            b.Property(x => x.RequestHash).HasMaxLength(200);
            b.Property(x => x.ResponseBody).HasColumnType("text");
            b.Property(x => x.StatusCode);
            b.Property(x => x.CreatedAt).IsRequired();

            b.HasIndex(x => new { x.Key, x.Route }).IsUnique();
        });
    }
}
