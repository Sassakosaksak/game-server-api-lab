using GameServerApi.Data;
using GameServerApi.Contracts;
using GameServerApi.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Npgsql;
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
var gameTimeZoneId = builder.Configuration["Game:TimeZoneId"] ?? "Asia/Tokyo";
var gameTimeZone = TimeZoneInfo.FindSystemTimeZoneById(gameTimeZoneId);
var shopItems = new Dictionary<string, ShopItem>(StringComparer.Ordinal)
{
    ["potion"] = new("potion", "Potion", 50),
    ["bronze-sword"] = new("bronze-sword", "Bronze Sword", 150),
    ["legendary-sword"] = new("legendary-sword", "Legendary Sword", 1000)
};

if (Encoding.UTF8.GetByteCount(jwtKey) < 32)
{
    throw new InvalidOperationException("JWT key must be at least 32 bytes.");
}

var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // JWTの標準クレーム名（subなど）を変換せず、そのまま扱う。
        options.MapInboundClaims = false;

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

app.MapPost("/rewards/daily-login/claim", async (ClaimsPrincipal user, GameDbContext db) =>
{
    const string rewardCode = "daily-login";
    const int rewardGold = 100;

    var playerIdText = user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
    if (!int.TryParse(playerIdText, out var playerId))
    {
        return Results.Unauthorized();
    }

    var player = await db.Players.FindAsync(playerId);
    if (player is null)
    {
        return Results.NotFound(new { message = "Player not found." });
    }

    var claimedAt = DateTime.UtcNow;
    // 報酬の対象日はクライアント値ではなく、ゲームサーバーのJST時刻で決める。
    var rewardDate = DateOnly.FromDateTime(
        TimeZoneInfo.ConvertTimeFromUtc(claimedAt, gameTimeZone));

    await using var transaction = await db.Database.BeginTransactionAsync();

    try
    {
        db.PlayerRewardClaims.Add(new PlayerRewardClaim
        {
            PlayerId = player.Id,
            RewardCode = rewardCode,
            RewardDate = rewardDate,
            GrantedGold = rewardGold,
            ClaimedAt = claimedAt
        });
        await db.SaveChangesAsync();

        player.Gold += rewardGold;
        await db.SaveChangesAsync();

        await transaction.CommitAsync();

        return Results.Ok(new
        {
            rewardCode,
            rewardDate,
            grantedGold = rewardGold,
            totalGold = player.Gold,
            claimedAt
        });
    }
    catch (DbUpdateException exception)
        when (exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation
        })
    {
        await transaction.RollbackAsync();

        return Results.Conflict(new
        {
            message = "This reward has already been claimed today.",
            rewardCode,
            rewardDate
        });
    }
}).RequireAuthorization();

app.MapGet("/shop/items", () => Results.Ok(shopItems.Values));

app.MapPost("/shop/items/{itemCode}/purchase", async (
    string itemCode,
    ClaimsPrincipal user,
    GameDbContext db) =>
{
    if (!shopItems.TryGetValue(itemCode, out var item))
    {
        return Results.NotFound(new { message = "Item not found." });
    }

    var playerIdText = user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
    if (!int.TryParse(playerIdText, out var playerId))
    {
        return Results.Unauthorized();
    }

    await using var transaction = await db.Database.BeginTransactionAsync();

    // 残高確認とGold減算を一つのUPDATEで行い、同時購入でも残高を超えて減らさない。
    var updatedPlayerCount = await db.Players
        .Where(player => player.Id == playerId && player.Gold >= item.PriceGold)
        .ExecuteUpdateAsync(setters => setters
            .SetProperty(player => player.Gold, player => player.Gold - item.PriceGold));

    if (updatedPlayerCount == 0)
    {
        await transaction.RollbackAsync();

        var playerExists = await db.Players.AnyAsync(player => player.Id == playerId);
        return playerExists
            ? Results.BadRequest(new { message = "Not enough gold." })
            : Results.NotFound(new { message = "Player not found." });
    }

    var purchasedAt = DateTime.UtcNow;
    db.PlayerInventoryItems.Add(new PlayerInventoryItem
    {
        PlayerId = playerId,
        ItemCode = item.Code,
        AcquiredAt = purchasedAt
    });
    db.PurchaseHistories.Add(new PurchaseHistory
    {
        PlayerId = playerId,
        ItemCode = item.Code,
        PriceGold = item.PriceGold,
        PurchasedAt = purchasedAt
    });
    await db.SaveChangesAsync();

    var totalGold = await db.Players
        .Where(player => player.Id == playerId)
        .Select(player => player.Gold)
        .SingleAsync();

    await transaction.CommitAsync();

    return Results.Ok(new
    {
        item = item.Code,
        priceGold = item.PriceGold,
        totalGold,
        purchasedAt
    });
}).RequireAuthorization();

app.MapGet("/players/me/items", async (ClaimsPrincipal user, GameDbContext db) =>
{
    var playerIdText = user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
    if (!int.TryParse(playerIdText, out var playerId))
    {
        return Results.Unauthorized();
    }

    var items = await db.PlayerInventoryItems
        .Where(item => item.PlayerId == playerId)
        .OrderByDescending(item => item.AcquiredAt)
        .ToListAsync();

    return Results.Ok(items);
}).RequireAuthorization();

app.Run();
