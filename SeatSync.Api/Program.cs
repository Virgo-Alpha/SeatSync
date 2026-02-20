using System.Reflection;
using Microsoft.EntityFrameworkCore;
using SeatSync.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<SeatSyncDbContext>(opt =>
{
    opt.UseSqlServer(builder.Configuration.GetConnectionString("SeatSyncDb"));
});

builder.Services.AddSwaggerGen(c =>
{
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    c.IncludeXmlComments(xmlPath);
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// TODO: The below causes "Missing XML comment for publicly visible type or member: {Model / Controller}"


app.UseHttpsRedirection();
app.MapControllers();
app.Run();