using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.Commons.Nio.Channels;
using Aion.GameServer.Commons.Network;
using Aion.GameServer.Configs.Network;
using Aion.GameServer.Network.Sequrity;

namespace Aion.GameServer.Network.Aion;

/// <summary>
/// Java parity: network/aion/GameConnectionFactoryImpl (-Nemesiss-). ConnectionFactory that creates
/// AionConnections (with optional FloodManager connection throttling). The faithful replacement for the
/// reworked GameClientSocketServer/GameServerConnection accept path, wired into NioServer via ServerCfg.
/// </summary>
public class GameConnectionFactoryImpl : ConnectionFactory
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(GameConnectionFactoryImpl));
    private readonly FloodManager floodAcceptor;

    public GameConnectionFactoryImpl()
    {
        if (NetworkConfig.ENABLE_FLOOD_CONNECTIONS)
        {
            floodAcceptor = new FloodManager(NetworkConfig.Flood_Tick,
                new FloodManager.FloodFilter(NetworkConfig.Flood_SWARN, NetworkConfig.Flood_SReject, NetworkConfig.Flood_STick), // short period
                new FloodManager.FloodFilter(NetworkConfig.Flood_LWARN, NetworkConfig.Flood_LReject, NetworkConfig.Flood_LTick)); // long period
        }
    }

    public AConnection Create(SocketChannel socket, Dispatcher dispatcher)
    {
        if (NetworkConfig.ENABLE_FLOOD_CONNECTIONS)
        {
            string host = socket.Socket().GetInetAddress().GetHostAddress();
            FloodManager.Result isFlooding = floodAcceptor.IsFlooding(host, true);
            switch (isFlooding)
            {
                case FloodManager.Result.REJECTED:
                    log.LogWarning("Rejected connection from " + host);
                    return null;
                case FloodManager.Result.WARNED:
                    log.LogWarning("Connection over warn limit from " + host);
                    break;
            }
        }

        return new AionConnection(socket, dispatcher);
    }
}
