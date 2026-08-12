using Microsoft.Extensions.Logging;

namespace GameServerApi.Logging;

internal static partial class GameLog
{
    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Information,
        Message = "HTTPリクエストが完了しました。TraceId: {TraceId}, Method: {Method}, Path: {Path}, StatusCode: {StatusCode}, ElapsedMs: {ElapsedMs}")]
    internal static partial void RequestCompleted(
        ILogger logger,
        string traceId,
        string method,
        string path,
        int statusCode,
        double elapsedMs);

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Error,
        Message = "HTTPリクエストの処理中に例外が発生しました。TraceId: {TraceId}, Method: {Method}, Path: {Path}")]
    internal static partial void RequestFailed(
        ILogger logger,
        string traceId,
        string method,
        string path,
        Exception exception);

    [LoggerMessage(
        EventId = 2000,
        Level = LogLevel.Information,
        Message = "デイリー報酬を付与しました。PlayerId: {PlayerId}, RewardCode: {RewardCode}, RewardDate: {RewardDate}, GrantedGold: {GrantedGold}, TotalGold: {TotalGold}")]
    internal static partial void DailyRewardClaimed(
        ILogger logger,
        int playerId,
        string rewardCode,
        DateOnly rewardDate,
        int grantedGold,
        int totalGold);

    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Warning,
        Message = "デイリー報酬は当日分を受取済みです。PlayerId: {PlayerId}, RewardCode: {RewardCode}, RewardDate: {RewardDate}")]
    internal static partial void DailyRewardAlreadyClaimed(
        ILogger logger,
        int playerId,
        string rewardCode,
        DateOnly rewardDate);

    [LoggerMessage(
        EventId = 2002,
        Level = LogLevel.Information,
        Message = "ショップアイテムを購入しました。PlayerId: {PlayerId}, ItemCode: {ItemCode}, PriceGold: {PriceGold}, TotalGold: {TotalGold}")]
    internal static partial void ShopItemPurchased(
        ILogger logger,
        int playerId,
        string itemCode,
        int priceGold,
        int totalGold);

    [LoggerMessage(
        EventId = 2003,
        Level = LogLevel.Warning,
        Message = "Gold不足のためショップアイテムを購入できませんでした。PlayerId: {PlayerId}, ItemCode: {ItemCode}, PriceGold: {PriceGold}")]
    internal static partial void ShopPurchaseRejectedForInsufficientGold(
        ILogger logger,
        int playerId,
        string itemCode,
        int priceGold);

    [LoggerMessage(
        EventId = 3000,
        Level = LogLevel.Information,
        Message = "ランキング更新通知用WebSocketに接続しました。ConnectionId: {ConnectionId}")]
    internal static partial void RankingWebSocketConnected(
        ILogger logger,
        string connectionId);

    [LoggerMessage(
        EventId = 3001,
        Level = LogLevel.Information,
        Message = "ランキング更新通知用WebSocketを切断しました。ConnectionId: {ConnectionId}")]
    internal static partial void RankingWebSocketDisconnected(
        ILogger logger,
        string connectionId);

    [LoggerMessage(
        EventId = 3002,
        Level = LogLevel.Information,
        Message = "ランキング更新をWebSocketへ通知しました。ConnectionCount: {ConnectionCount}")]
    internal static partial void RankingUpdateNotified(
        ILogger logger,
        int connectionCount);

    [LoggerMessage(
        EventId = 3003,
        Level = LogLevel.Warning,
        Message = "ランキング更新通知用WebSocketの接続中に例外が発生しました。ConnectionId: {ConnectionId}")]
    internal static partial void RankingWebSocketConnectionFailed(
        ILogger logger,
        string connectionId,
        Exception exception);

    [LoggerMessage(
        EventId = 3004,
        Level = LogLevel.Warning,
        Message = "ランキング更新通知用WebSocketへの送信に失敗しました。ConnectionId: {ConnectionId}")]
    internal static partial void RankingWebSocketNotificationFailed(
        ILogger logger,
        string connectionId,
        Exception exception);
}
