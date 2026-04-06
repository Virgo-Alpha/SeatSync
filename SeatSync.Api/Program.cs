using System.Reflection;
using Microsoft.EntityFrameworkCore;
using SeatSync.Domain.Entities;
using SeatSync.Domain.Enums;
using SeatSync.Infrastructure.Data;
using SeatSync.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IReservationStoredProcedureService, ReservationStoredProcedureService>();

if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddDbContext<SeatSyncDbContext>(opt =>
        opt.UseSqlServer(builder.Configuration.GetConnectionString("SeatSyncDb")));
}
else
{
    builder.Services.AddDbContext<SeatSyncDbContext>(opt =>
        opt.UseSqlServer(builder.Configuration.GetConnectionString("SeatSyncDb")));
}

builder.Services.AddSwaggerGen(c =>
{
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    c.IncludeXmlComments(xmlPath);
});

var app = builder.Build();

if (!app.Environment.IsEnvironment("Testing"))
{
    // Compose startup can race SQL initialization; retry migrations so "docker compose up -d"
    // can provision the schema without manual EF commands.
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<SeatSyncDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
        .CreateLogger("DatabaseStartup");

    const int maxAttempts = 10;
    for (var attempt = 1; attempt <= maxAttempts; attempt++)
    {
        try
        {
            dbContext.Database.Migrate();
            await EnsureSeedDataAsync(dbContext);
            break;
        }
        catch (Exception ex) when (attempt < maxAttempts)
        {
            logger.LogWarning(
                ex,
                "Database migration attempt {Attempt}/{MaxAttempts} failed. Retrying...",
                attempt,
                maxAttempts);

            await Task.Delay(TimeSpan.FromSeconds(5));
        }
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// TODO: The below causes "Missing XML comment for publicly visible type or member: {Model / Controller}"


app.UseHttpsRedirection();
app.MapControllers();
app.Run();

static async Task EnsureSeedDataAsync(SeatSyncDbContext dbContext)
{
    if (await dbContext.Events.AnyAsync())
    {
        return;
    }

    var seededEvent = new Event("SeatSync Demo Event", DateTimeOffset.UtcNow.AddDays(10));
    dbContext.Events.Add(seededEvent);

    var rows = new[] { "A", "B", "C", "D", "E", "F", "G" };
    for (var rowIndex = 0; rowIndex < rows.Length; rowIndex++)
    {
        var row = rows[rowIndex];
        var rowPosition = rowIndex + 1;
        for (var seatNumber = 1; seatNumber <= 12; seatNumber++)
        {
            var seat = new Seat(
                seededEvent.Id,
                "Orchestra",
                row,
                seatNumber.ToString(),
                seatNumber,
                rowPosition);

            dbContext.Seats.Add(seat);
            dbContext.SeatStatuses.Add(new SeatStatus(
                seededEvent.Id,
                seat.Id,
                SeatState.Available));
        }
    }

    await dbContext.SaveChangesAsync();
}
