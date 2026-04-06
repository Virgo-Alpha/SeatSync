using System.Reflection;
using Microsoft.EntityFrameworkCore;
using SeatSync.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton(TimeProvider.System);

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
