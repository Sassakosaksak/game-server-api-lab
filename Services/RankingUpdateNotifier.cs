using GameServerApi.Logging;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text.Json;

namespace GameServerApi.Services;

public sealed class RankingUpdateNotifier
{
    private readonly ConcurrentDictionary<string, WebSocket> _connections = new();
    private readonly ILogger<RankingUpdateNotifier> _logger;

    public RankingUpdateNotifier(ILogger<RankingUpdateNotifier> logger)
    {
        _logger = logger;
    }

    public async Task WaitForDisconnectAsync(WebSocket socket, string connectionId)
    {
        _connections[connectionId] = socket;
        GameLog.RankingWebSocketConnected(_logger, connectionId);

        var receiveBuffer = new byte[1024];

        try
        {
            while (socket.State == WebSocketState.Open)
            {
                // 接続中のクライアントから届くデータを読み取り、切断通知を検知できるようにする。
                var result = await socket.ReceiveAsync(receiveBuffer, CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }
            }
        }
        catch (WebSocketException exception)
        {
            GameLog.RankingWebSocketConnectionFailed(_logger, connectionId, exception);
        }
        finally
        {
            _connections.TryRemove(connectionId, out _);
            GameLog.RankingWebSocketDisconnected(_logger, connectionId);
        }
    }

    public async Task NotifyRankingsUpdatedAsync()
    {
        var notification = JsonSerializer.SerializeToUtf8Bytes(new
        {
            type = "rankings-updated"
        });
        var notifiedConnectionCount = 0;

        foreach (var (connectionId, socket) in _connections)
        {
            if (socket.State != WebSocketState.Open)
            {
                _connections.TryRemove(connectionId, out _);
                continue;
            }

            try
            {
                await socket.SendAsync(
                    notification,
                    WebSocketMessageType.Text,
                    endOfMessage: true,
                    CancellationToken.None);
                notifiedConnectionCount++;
            }
            catch (WebSocketException exception)
            {
                // 切断済みクライアントへの送信失敗で、ランキング更新API全体を失敗させない。
                GameLog.RankingWebSocketNotificationFailed(_logger, connectionId, exception);
                _connections.TryRemove(connectionId, out _);
            }
        }

        if (notifiedConnectionCount > 0)
        {
            GameLog.RankingUpdateNotified(_logger, notifiedConnectionCount);
        }
    }
}
