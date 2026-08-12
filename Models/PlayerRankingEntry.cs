namespace GameServerApi.Models;

public record PlayerRankingEntry(
    int Rank,
    int PlayerId,
    string Name,
    int Level,
    int Gold);
