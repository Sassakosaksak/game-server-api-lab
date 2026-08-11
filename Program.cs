using GameServerApi.Data;
using GameServerApi.Contracts;
using GameServerApi.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<GameDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("GameDb")));

builder.Services.AddValidation();

var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("JWT key is not configured.");
var jwtIssuer = builder.Configuration["Jwt:Issuer"]
    ?? throw new InvalidOperationException("JWT issuer is not configured.");
var jwtAudience = builder.Configuration["Jwt:Audience"]
    ?? throw new InvalidOperationException("JWT audience is not configured.");

if (Encoding.UTF8.GetByteCount(jwtKey) < 32)
{
    throw new InvalidOperationException("JWT key must be at least 32 bytes.");
}

var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "JWTを入力すると、認証が必要なAPIをSwaggerから実行できます。"
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("bearer", document)] = []
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
    await db.Database.MigrateAsync();
}

app.UseSwagger();
app.UseSwaggerUI();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => new
{
    status = "ok",
    time = DateTime.UtcNow
});

// 開発学習用: 指定したプレイヤーとしてJWTを発行する。本番ではID・パスワード等の本人確認が必要。
app.MapPost("/auth/dev-login", async (LoginRequest request, GameDbContext db) =>
{
    var player = await db.Players.FindAsync(request.PlayerId);
    if (player is null)
    {
        return Results.NotFound(new { message = "Player not found." });
    }

    var now = DateTime.UtcNow;
    var token = new JwtSecurityToken(
        issuer: jwtIssuer,
        audience: jwtAudience,
        claims:
        [
            new Claim(JwtRegisteredClaimNames.Sub, player.Id.ToString()),
            new Claim(ClaimTypes.Name, player.Name)
        ],
        notBefore: now,
        expires: now.AddHours(1),
        signingCredentials: new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256));

    return Results.Ok(new
    {
        accessToken = new JwtSecurityTokenHandler().WriteToken(token),
        expiresAt = token.ValidTo
    });
}).AllowAnonymous();

app.MapPost("/players", async (CreatePlayerRequest request, GameDbContext db) =>
{
    var player = new Player
    {
        Name = request.Name,
        Level = request.Level,
        Gold = request.Gold
    };

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

app.MapGet("/players/me", async (ClaimsPrincipal user, GameDbContext db) =>
{
    var playerIdText = user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
    if (!int.TryParse(playerIdText, out var playerId))
    {
        return Results.Unauthorized();
    }

    var player = await db.Players.FindAsync(playerId);
    return player is null
        ? Results.NotFound()
        : Results.Ok(player);
}).RequireAuthorization();

app.Run();
