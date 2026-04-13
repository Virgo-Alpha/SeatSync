using Microsoft.EntityFrameworkCore;
using SeatSync.Domain.Entities;

namespace SeatSync.Infrastructure.Data;

public sealed class SeatSyncDbContext : DbContext
{
    public SeatSyncDbContext(DbContextOptions<SeatSyncDbContext> options) : base(options) {}

    public DbSet<Event> Events => Set<Event>();
    public DbSet<Seat> Seats => Set<Seat>();
    public DbSet<SeatStatus> SeatStatuses => Set<SeatStatus>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<Hold> Holds => Set<Hold>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<AppUser> AppUsers => Set<AppUser>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Event>(b =>
        {
            b.ToTable("Events");
            b.HasKey(x => x.Id);
            b.Property(x => x.Name).IsRequired().HasMaxLength(200);
            b.Property(x => x.StartsAt).IsRequired();
            b.Property(x => x.Agenda).HasMaxLength(2000);
            b.Property(x => x.CreatedByUserId);

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
            b.Property(x => x.X).HasColumnType("decimal(18,2)");
            b.Property(x => x.Y).HasColumnType("decimal(18,2)");

            // Optional: b.HasIndex("EventId", nameof(Seat.Section), nameof(Seat.Row), nameof(Seat.Number)).IsUnique();
        });

        modelBuilder.Entity<Order>(b =>
        {
            b.Property(x => x.TotalAmount).HasColumnType("decimal(18,2)");
        });

        modelBuilder.Entity<AppUser>(b =>
        {
            b.ToTable("AppUsers");
            b.HasKey(x => x.Id);
            b.Property(x => x.Email).IsRequired().HasMaxLength(200);
            b.Property(x => x.DisplayName).IsRequired().HasMaxLength(200);
            b.Property(x => x.Password).IsRequired().HasMaxLength(200);
            b.Property(x => x.Role).IsRequired();
            b.Property(x => x.CreatedAt).IsRequired();
            b.HasIndex(x => x.Email).IsUnique();
        });
    }
}
