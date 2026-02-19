using Microsoft.EntityFrameworkCore;
using SeatSync.Domain.Entities;

namespace SeatSync.Infrastructure.Data;

public sealed class SeatSyncDbContext : DbContext
{
    public SeatSyncDbContext(DbContextOptions<SeatSyncDbContext> options) : base(options) {}

    public DbSet<Event> Events => Set<Event>();
    public DbSet<Seat> Seats => Set<Seat>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Event>(b =>
        {
            b.ToTable("Events");
            b.HasKey(x => x.Id);
            b.Property(x => x.Name).IsRequired().HasMaxLength(200);
            b.Property(x => x.StartsAt).IsRequired();

            b.HasMany(x => x.Seats)
                .WithOne()
                .HasForeignKey("EventId")
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Seat>(b =>
        {
            b.ToTable("Seats");
            b.HasKey(x => x.Id);

            b.Property<Guid>("EventId");

            b.Property(x => x.Section).HasMaxLength(50);
            b.Property(x => x.Row).HasMaxLength(20);
            b.Property(x => x.Number).HasMaxLength(20);

            // Optional: b.HasIndex("EventId", nameof(Seat.Section), nameof(Seat.Row), nameof(Seat.Number)).IsUnique();
        });
    }
}