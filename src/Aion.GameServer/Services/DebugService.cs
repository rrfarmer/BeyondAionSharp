using System;
using System.Threading.Tasks;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Services;

/// <summary>Java parity: services/DebugService (ATracer).</summary>
public class DebugService
{
    private static readonly ILogger log = NullLogger.Instance;

    private const int ANALYZE_PLAYERS_INTERVAL = 30 * 60 * 1000;

    public static DebugService GetInstance()
    {
        return SingletonHolder.instance;
    }

    private DebugService()
    {
        ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(ct =>
        {
            AnalyzeWorldPlayers();
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(ANALYZE_PLAYERS_INTERVAL), TimeSpan.FromMilliseconds(ANALYZE_PLAYERS_INTERVAL));
        log.LogInformation("DebugService started. Analyze interval: " + ANALYZE_PLAYERS_INTERVAL);
    }

    private void AnalyzeWorldPlayers()
    {
        log.LogInformation("Starting analysis of world players");

        foreach (Player player in Aion.GameServer.World.World.GetInstance().GetAllPlayers())
        {
            // Check connection
            AionConnection connection = player.GetClientConnection();
            if (connection == null)
            {
                log.LogWarning("[DEBUG SERVICE] Found {Player} without connection: Spawned {Spawned}", player, player.IsSpawned());
                continue;
            }

            long lastPing = connection.GetLastPingTime();
            if (lastPing > 0)
            {
                long pingInterval = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - lastPing;
                if (pingInterval - 5000 > CmPing.CLIENT_PING_INTERVAL)
                    log.LogWarning("[DEBUG SERVICE] Found {Player} with large ping interval: Spawned {Spawned}, PingMS {PingMS}", player, player.IsSpawned(), pingInterval);
            }
        }

        log.LogInformation("Analysis of world players finished");
    }

    private static class SingletonHolder
    {
        internal static readonly DebugService instance = new DebugService();
    }
}
