using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.IdentityModel.Tokens;
using SeatSync.Api.Auth;
using SeatSync.Domain.Entities;
using SeatSync.Domain.Enums;
using SeatSync.Infrastructure.Data;
using SeatSync.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IReservationStoredProcedureService, ReservationStoredProcedureService>();
builder.Services.AddScoped<ITokenService, JwtTokenService>();
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));

var jwtOptions = builder.Configuration
    .GetSection(JwtOptions.SectionName)
    .Get<JwtOptions>() ?? new JwtOptions();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey))
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddDbContext<SeatSyncDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("SeatSyncDb"))
        .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning)));

builder.Services.AddSwaggerGen(c =>
{
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    c.IncludeXmlComments(xmlPath);
});

var app = builder.Build();

if (!app.Environment.IsEnvironment("Testing"))
{
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

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();

static async Task EnsureSeedDataAsync(SeatSyncDbContext dbContext)
{
    if (!await dbContext.AppUsers.AnyAsync())
    {
        dbContext.AppUsers.AddRange(
            new AppUser(
                id: Guid.Parse("3ce0af30-44f7-4a5e-9b25-3ca672ebd5bb"),
                email: "admin@seatsync.demo",
                displayName: "SeatSync Admin",
                password: "demo123",
                role: UserRole.Admin),
            new AppUser(
                id: Guid.Parse("f8764ebb-7e22-40af-abf6-145bcf58f3a3"),
                email: "organizer@seatsync.demo",
                displayName: "Event Organizer",
                password: "demo123",
                role: UserRole.Organizer),
            new AppUser(
                id: Guid.Parse("ec880f1f-8a06-419f-97d0-68c3ef548b15"),
                email: "attendee@seatsync.demo",
                displayName: "Demo Attendee",
                password: "demo123",
                role: UserRole.Attendee));
    }

    if (!await dbContext.Events.AnyAsync())
    {
        var seededEvent = new Event(
            "SeatSync Demo Event",
            DateTimeOffset.UtcNow.AddDays(10),
            "Doors open 18:30. Main act starts 19:00.",
            Guid.Parse("f8764ebb-7e22-40af-abf6-145bcf58f3a3"));

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
    }

    await dbContext.SaveChangesAsync();
}
