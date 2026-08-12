using GameServerApi.Data;
using GameServerApi.Models;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using System.Text.Json;

namespace GameServerApi.Services;

public class RankingCacheService
{
    private static readonly TimeSpan RankingCacheTtl = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan VersionCacheTtl = TimeSpan.FromDays(1);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IDatabase _redis;
    private readonly ILogger<RankingCacheService> _logger;

    public RankingCacheService(
        IConnectionMultiplexer connectionMultiplexer,
        ILogger<RankingCacheService> logger)
    {
        _redis = connectionMultiplexer.GetDatabase();
        _logger = logger;
    }

    public async Task<RankingCacheResult> GetRankingsAsync(int top, GameDbContext db)
    {
        try
        {
            var cacheVersion = await GetCacheVersionAsync();
            var cacheKey = GetRankingCacheKey(top, cacheVersion);
            var cachedValue = await _redis.StringGetAsync(cacheKey);

            if (!cachedValue.IsNullOrEmpty)
            {
                var cachedRankings = JsonSerializer.Deserialize<List<PlayerRankingEntry>>(
                    cachedValue.ToString(),
                    JsonOptions);
                if (cachedRankings is not null)
                {
                    return new RankingCacheResult(cachedRankings, true);
                }

                // 壊れたキャッシュは削除し、DBから正しい値を読み直す。
                await _redis.KeyDeleteAsync(cacheKey);
            }
        }
        catch (RedisException exception)
        {
            // Redis障害時も、PostgreSQLからランキングを取得できるようにする。
            _logger.LogWarning(exception, "Redisからランキングキャッシュを取得できませんでした。");
        }
        catch (JsonException exception)
        {
            _logger.LogWarning(exception, "ランキングキャッシュのJSONを読み取れませんでした。");
        }

        var rankingCandidates = await db.Players
            .AsNoTracking()
            .OrderByDescending(player => player.Gold)
            .ThenByDescending(player => player.Level)
            .ThenBy(player => player.Id)
            .Take(top)
            .Select(player => new PlayerRankingEntry(
                0,
                player.Id,
                player.Name,
                player.Level,
                player.Gold))
            .ToListAsync();

        var rankings = rankingCandidates
            .Select((player, index) => player with { Rank = index + 1 })
            .ToList();

        try
        {
            var cacheVersion = await GetCacheVersionAsync();
            var cacheKey = GetRankingCacheKey(top, cacheVersion);
            var serializedRankings = JsonSerializer.Serialize(rankings, JsonOptions);

            await _redis.StringSetAsync(cacheKey, serializedRankings, RankingCacheTtl);
        }
        catch (RedisException exception)
        {
            _logger.LogWarning(exception, "Redisへランキングキャッシュを保存できませんでした。");
        }

        return new RankingCacheResult(rankings, false);
    }

    public async Task InvalidateRankingsAsync()
    {
        try
        {
            // 世代を切り替えることで、topごとの古いランキングをまとめて読ませなくする。
            await _redis.StringSetAsync(
                GetVersionCacheKey(),
                CreateCacheVersion(),
                VersionCacheTtl);

            _logger.LogInformation("ランキングキャッシュを無効化しました。");
        }
        catch (RedisException exception)
        {
            // DB更新はすでに確定しているため、ランキングはTTLによる自然更新に任せる。
            _logger.LogWarning(exception, "Redisのランキングキャッシュを無効化できませんでした。");
        }
    }

    private async Task<string> GetCacheVersionAsync()
    {
        var versionKey = GetVersionCacheKey();
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

        var concurrentVersion = await _redis.StringGetAsync(versionKey);
        return concurrentVersion.IsNullOrEmpty ? newVersion : concurrentVersion!;
    }

    private static string GetVersionCacheKey() => "ranking:players:cache-version";

    private static string GetRankingCacheKey(int top, string cacheVersion) =>
        $"ranking:players:top:{top}:{cacheVersion}";

    private static string CreateCacheVersion() => Guid.NewGuid().ToString("N");
}

public record RankingCacheResult(IReadOnlyList<PlayerRankingEntry> Rankings, bool FromCache);
