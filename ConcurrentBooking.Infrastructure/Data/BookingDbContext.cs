using Microsoft.EntityFrameworkCore;
using ConcurrentBooking.Domain.Entities;

namespace ConcurrentBooking.Infrastructure.Data;

public class BookingDbContext : DbContext
{
    public BookingDbContext(DbContextOptions<BookingDbContext> options) : base(options)
    {
    }

    public DbSet<Specialty> Specialties { get; set; } = null!;
    public DbSet<ClinicUnit> ClinicUnits { get; set; } = null!;
    public DbSet<Professional> Professionals { get; set; } = null!;
    public DbSet<Slot> Slots { get; set; } = null!;
    public DbSet<Hold> Holds { get; set; } = null!;
    public DbSet<Booking> Bookings { get; set; } = null!;
    public DbSet<IdempotencyRecord> IdempotencyRecords { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Specialty>(b =>
        {
            b.ToTable("specialties");
            b.HasKey(x => x.Id);
            b.Property(x => x.Name).IsRequired().HasMaxLength(200);
            b.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<ClinicUnit>(b =>
        {
            b.ToTable("clinic_units");
            b.HasKey(x => x.Id);
            b.Property(x => x.Name).IsRequired().HasMaxLength(200);
            b.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<Professional>(b =>
        {
            b.ToTable("professionals");
            b.HasKey(x => x.Id);
            b.Property(x => x.SpecialtyId).IsRequired();
            b.Property(x => x.FullName).IsRequired().HasMaxLength(200);
            b.HasIndex(x => new { x.SpecialtyId, x.FullName });
            b.HasOne<Specialty>()
                .WithMany()
                .HasForeignKey(x => x.SpecialtyId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Slot>(b =>
        {
            b.ToTable("slots");
            b.HasKey(x => x.Id);
            b.Property(x => x.ProfessionalId).IsRequired();
            b.Property(x => x.ClinicUnitId).IsRequired();
            b.Property(x => x.StartsAt).IsRequired();
            b.Property(x => x.EndsAt).IsRequired();
            b.Property(x => x.SeatCode).HasMaxLength(50);
            b.HasIndex(x => new { x.ProfessionalId, x.ClinicUnitId, x.StartsAt });
            b.HasOne<Professional>()
                .WithMany()
                .HasForeignKey(x => x.ProfessionalId)
                .OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ClinicUnit>()
                .WithMany()
                .HasForeignKey(x => x.ClinicUnitId)
                .OnDelete(DeleteBehavior.Restrict);
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
            b.HasIndex("SlotId").HasDatabaseName("idx_holds_slot_active").HasFilter("\"Status\" = 0").IsUnique();
            b.HasOne<Slot>()
                .WithMany()
                .HasForeignKey(x => x.SlotId)
                .OnDelete(DeleteBehavior.Cascade);
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
            b.HasOne<Slot>()
                .WithMany()
                .HasForeignKey(x => x.SlotId)
                .OnDelete(DeleteBehavior.Cascade);
            b.HasOne<Hold>()
                .WithMany()
                .HasForeignKey(x => x.HoldId)
                .OnDelete(DeleteBehavior.Restrict);
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
