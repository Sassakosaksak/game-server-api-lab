using GameServerApi.Data;
using Microsoft.EntityFrameworkCore;
using GameServerApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<GameDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("GameDb")));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
    await db.Database.MigrateAsync();
}

app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/health", () => new
{
    status = "ok",
    time = DateTime.UtcNow
});

app.MapPost("/players", async (Player player, GameDbContext db) =>
{
    db.Players.Add(player);
    await db.SaveChangesAsync();

    return Results.Created($"/players/{player.Id}", player);
});

app.MapGet("/players", async (GameDbContext db) =>
{
    var players = await db.Players.ToListAsync();
    return Results.Ok(players);
});

app.MapGet("/players/{id:int}", async (int id, GameDbContext db) =>
{
    var player = await db.Players.FindAsync(id);

    return player is null
        ? Results.NotFound()
        : Results.Ok(player);
});

app.Run();
