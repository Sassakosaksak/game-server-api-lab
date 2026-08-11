using GameServerApi.Data;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using System.Text.Json;

namespace GameServerApi.Services;

public class PlayerCacheService
{
    private static readonly TimeSpan ProfileCacheTtl = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan VersionCacheTtl = TimeSpan.FromDays(1);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IDatabase _redis;
    private readonly ILogger<PlayerCacheService> _logger;

    public PlayerCacheService(
        IConnectionMultiplexer connectionMultiplexer,
        ILogger<PlayerCacheService> logger)
    {
        _redis = connectionMultiplexer.GetDatabase();
        _logger = logger;
    }

    public async Task<PlayerCacheResult> GetPlayerAsync(int playerId, GameDbContext db)
    {
        try
        {
            var cacheVersion = await GetCacheVersionAsync(playerId);
            var cacheKey = GetProfileCacheKey(playerId, cacheVersion);
            var cachedValue = await _redis.StringGetAsync(cacheKey);

            if (!cachedValue.IsNullOrEmpty)
            {
                var cachedPlayer = JsonSerializer.Deserialize<PlayerCacheEntry>(cachedValue.ToString(), JsonOptions);
                if (cachedPlayer is not null)
                {
                    return new PlayerCacheResult(cachedPlayer, true);
                }

                // 壊れたキャッシュは削除し、DBから正しい値を読み直す。
                await _redis.KeyDeleteAsync(cacheKey);
            }
        }
        catch (RedisException exception)
        {
            // Redis障害時も、正本であるPostgreSQLから読み取れるようにする。
            _logger.LogWarning(exception, "Redisからプレイヤーキャッシュを取得できませんでした。 PlayerId: {PlayerId}", playerId);
        }
        catch (JsonException exception)
        {
            _logger.LogWarning(exception, "プレイヤーキャッシュのJSONを読み取れませんでした。 PlayerId: {PlayerId}", playerId);
        }

        var player = await db.Players
            .AsNoTracking()
            .Where(player => player.Id == playerId)
            .Select(player => new PlayerCacheEntry(
                player.Id,
                player.Name,
                player.Level,
                player.Gold))
            .SingleOrDefaultAsync();

        if (player is null)
        {
            return new PlayerCacheResult(null, false);
        }

        try
        {
            var cacheVersion = await GetCacheVersionAsync(playerId);
            var cacheKey = GetProfileCacheKey(playerId, cacheVersion);
            var serializedPlayer = JsonSerializer.Serialize(player, JsonOptions);

            await _redis.StringSetAsync(cacheKey, serializedPlayer, ProfileCacheTtl);
        }
        catch (RedisException exception)
        {
            _logger.LogWarning(exception, "Redisへプレイヤーキャッシュを保存できませんでした。 PlayerId: {PlayerId}", playerId);
        }

        return new PlayerCacheResult(player, false);
    }

    public async Task InvalidatePlayerAsync(int playerId)
    {
        try
        {
            // 新しい世代を設定すると、古い世代の関連キャッシュは読まれなくなる。
            await _redis.StringSetAsync(
                GetVersionCacheKey(playerId),
                CreateCacheVersion(),
                VersionCacheTtl);

            _logger.LogInformation("プレイヤーキャッシュを無効化しました。 PlayerId: {PlayerId}", playerId);
        }
        catch (RedisException exception)
        {
            // DB更新はすでに確定しているため、失敗時はTTLによる自然更新に任せる。
            _logger.LogWarning(exception, "Redisのプレイヤーキャッシュを無効化できませんでした。 PlayerId: {PlayerId}", playerId);
        }
    }

    private async Task<string> GetCacheVersionAsync(int playerId)
    {
        var versionKey = GetVersionCacheKey(playerId);
        var existingVersion = await _redis.StringGetAsync(versionKey);
        if (!existingVersion.IsNullOrEmpty)
        {
            return existingVersion!;
        }

        var newVersion = CreateCacheVersion();
        var created = await _redis.StringSetAsync(
            versionKey,
            newVersion,
            VersionCacheTtl,
            When.NotExists);

        if (created)
        {
            return newVersion;
        }

        // 同時リクエストが先に世代を作成した場合は、その値を使う。
        var concurrentVersion = await _redis.StringGetAsync(versionKey);
        return concurrentVersion.IsNullOrEmpty ? newVersion : concurrentVersion!;
    }

    private static string GetVersionCacheKey(int playerId) => $"player:{playerId}:cache-version";

    private static string GetProfileCacheKey(int playerId, string cacheVersion) =>
        $"player:{playerId}:profile:{cacheVersion}";

    private static string CreateCacheVersion() => Guid.NewGuid().ToString("N");
}

public record PlayerCacheEntry(int Id, string Name, int Level, int Gold);

public record PlayerCacheResult(PlayerCacheEntry? Player, bool FromCache);
