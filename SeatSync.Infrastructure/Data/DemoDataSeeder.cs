using Microsoft.EntityFrameworkCore;
using SeatSync.Domain.Entities;
using SeatSync.Domain.Enums;

namespace SeatSync.Infrastructure.Data;

public static class DemoDataSeeder
{
    private static readonly Guid AdminUserId = Guid.Parse("3ce0af30-44f7-4a5e-9b25-3ca672ebd5bb");
    private static readonly Guid OrganizerUserId = Guid.Parse("f8764ebb-7e22-40af-abf6-145bcf58f3a3");
    private static readonly Guid AttendeeOneId = Guid.Parse("ec880f1f-8a06-419f-97d0-68c3ef548b15");
    private static readonly Guid AttendeeTwoId = Guid.Parse("89a46df1-b098-45d7-81b8-7d6ab4029c6a");
    private static readonly Guid AttendeeThreeId = Guid.Parse("4104af7f-fbb8-4eb8-95f8-e8f8e04f9b84");

    public static async Task SeedAsync(SeatSyncDbContext dbContext, TimeProvider clock, CancellationToken ct = default)
    {
        await SeedUsersAsync(dbContext, ct);
        await SeedEventsAndSeatsAsync(dbContext, clock, ct);
    }

    private static async Task SeedUsersAsync(SeatSyncDbContext dbContext, CancellationToken ct)
    {
        var existingUsers = await dbContext.AppUsers
            .AsNoTracking()
            .Select(x => x.Email)
            .ToHashSetAsync(ct);

        var usersToInsert = new List<AppUser>();

        if (!existingUsers.Contains("admin@seatsync.demo"))
        {
            usersToInsert.Add(new AppUser(
                AdminUserId,
                "admin@seatsync.demo",
                "SeatSync Admin",
                "demo123",
                UserRole.Admin));
        }

        if (!existingUsers.Contains("organizer@seatsync.demo"))
        {
            usersToInsert.Add(new AppUser(
                OrganizerUserId,
                "organizer@seatsync.demo",
                "Event Organizer",
                "demo123",
                UserRole.Organizer));
        }

        if (!existingUsers.Contains("attendee@seatsync.demo"))
        {
            usersToInsert.Add(new AppUser(
                AttendeeOneId,
                "attendee@seatsync.demo",
                "Demo Attendee One",
                "demo123",
                UserRole.Attendee));
        }

        if (!existingUsers.Contains("attendee2@seatsync.demo"))
        {
            usersToInsert.Add(new AppUser(
                AttendeeTwoId,
                "attendee2@seatsync.demo",
                "Demo Attendee Two",
                "demo123",
                UserRole.Attendee));
        }

        if (!existingUsers.Contains("attendee3@seatsync.demo"))
        {
            usersToInsert.Add(new AppUser(
                AttendeeThreeId,
                "attendee3@seatsync.demo",
                "Demo Attendee Three",
                "demo123",
                UserRole.Attendee));
        }

        if (usersToInsert.Count > 0)
        {
            dbContext.AppUsers.AddRange(usersToInsert);
            await dbContext.SaveChangesAsync(ct);
        }
    }

    private static async Task SeedEventsAndSeatsAsync(SeatSyncDbContext dbContext, TimeProvider clock, CancellationToken ct)
    {
        var eventDefinitions = new[]
        {
            new EventSeed(
                "SeatSync Summer Night",
                DateTimeOffset.UtcNow.AddDays(14),
                "Live showcase with warm-up acts and visual stage production.",
                ("A", "1", AttendeeOneId),
                ("A", "2", AttendeeOneId),
                ("B", "4", AttendeeTwoId)),
            new EventSeed(
                "SeatSync Product Launch",
                DateTimeOffset.UtcNow.AddDays(30),
                "Product reveal keynote with networking lounge access.",
                ("A", "3", AttendeeThreeId),
                ("C", "6", AttendeeTwoId)),
            new EventSeed(
                "SeatSync Community Meetup",
                DateTimeOffset.UtcNow.AddDays(45),
                "Panel discussion and open Q&A with the engineering team.")
        };

        foreach (var definition in eventDefinitions)
        {
            var ev = await dbContext.Events
                .SingleOrDefaultAsync(x => x.Name == definition.Name, ct);

            if (ev is null)
            {
                ev = new Event(definition.Name, definition.StartsAtUtc, definition.Agenda, OrganizerUserId);
                dbContext.Events.Add(ev);
                await dbContext.SaveChangesAsync(ct);
            }

            var seatsForEvent = await dbContext.Seats
                .Where(x => x.EventId == ev.Id)
                .ToListAsync(ct);

            if (seatsForEvent.Count == 0)
            {
                var generatedSeats = CreateDefaultSeatMap(ev.Id);
                dbContext.Seats.AddRange(generatedSeats);
                dbContext.SeatStatuses.AddRange(generatedSeats.Select(seat =>
                    new SeatStatus(ev.Id, seat.Id, SeatState.Available)));
                await dbContext.SaveChangesAsync(ct);

                seatsForEvent = generatedSeats;
            }

            if (definition.PreBookedSeats.Length == 0)
            {
                continue;
            }

            var seatByLabel = seatsForEvent.ToDictionary(
                x => $"{x.Row}{x.Number}",
                StringComparer.OrdinalIgnoreCase);

            foreach (var booking in definition.PreBookedSeats)
            {
                var label = $"{booking.Row}{booking.Number}";
                if (!seatByLabel.TryGetValue(label, out var seat))
                {
                    continue;
                }

                var seatStatus = await dbContext.SeatStatuses
                    .SingleAsync(x => x.EventId == ev.Id && x.SeatId == seat.Id, ct);

                if (seatStatus.State == SeatState.Sold)
                {
                    continue;
                }

                if (seatStatus.State != SeatState.Available)
                {
                    continue;
                }

                var idempotencyKey = $"seed-{ev.Id:N}-{booking.UserId:N}-{label.ToLowerInvariant()}";

                var existingOrder = await dbContext.Orders
                    .SingleOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey, ct);

                if (existingOrder is null)
                {
                    existingOrder = new Order(
                        ev.Id,
                        booking.UserId,
                        totalAmount: 49.00m,
                        currency: "USD",
                        idempotencyKey: idempotencyKey,
                        seatIds: [seat.Id]);
                    existingOrder.MarkAuthorized();
                    existingOrder.MarkCaptured(clock);
                    dbContext.Orders.Add(existingOrder);
                }

                var holdId = Guid.NewGuid();
                seatStatus.MarkHeld(holdId);
                seatStatus.MarkSold(existingOrder.Id);
            }

            await dbContext.SaveChangesAsync(ct);
        }
    }

    private static List<Seat> CreateDefaultSeatMap(Guid eventId)
    {
        var rows = new[] { "A", "B", "C", "D", "E" };
        var seats = new List<Seat>();

        for (var rowIndex = 0; rowIndex < rows.Length; rowIndex++)
        {
            var row = rows[rowIndex];
            var y = rowIndex + 1;

            for (var seatNumber = 1; seatNumber <= 10; seatNumber++)
            {
                seats.Add(new Seat(
                    eventId,
                    "Main Floor",
                    row,
                    seatNumber.ToString(),
                    seatNumber,
                    y));
            }
        }

        return seats;
    }

    private sealed record EventSeed(
        string Name,
        DateTimeOffset StartsAtUtc,
        string Agenda,
        params (string Row, string Number, Guid UserId)[] PreBookedSeats);
}
