using System;
using System.Collections.Generic;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils.Audit;
using State = global::Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_PING (-Nemesiss-, Undertrey, Neon). Heartbeat ping; replies SM_PONG and detects time/speed hacks via ping-interval analysis. SM_PONG/AuditLogger red-tolerated.</summary>
public class CM_PING : AionClientPacket
{
    public const int CLIENT_PING_INTERVAL = 180 * 1000; // client sends this packet every 180 seconds

    public CM_PING(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        ReadH(); // unk
    }

    protected override void RunImpl()
    {
        long lastPingMillis = GetConnection().GetLastPingTime();
        long nowMillis = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        GetConnection().SetLastPingTime(nowMillis);
        SendPacket(new SM_PONG());

        if (lastPingMillis > 0)
        {
            long pingInterval = nowMillis - lastPingMillis;
            if (pingInterval + 2000 < CLIENT_PING_INTERVAL)
            { // client timer cheat
                if (GetConnection().IncreaseAndGetPingFailCount() == 3)
                {
                    if (SecurityConfig.PINGCHECK_KICK)
                    {
                        AuditLogger.Log(GetConnection().GetActivePlayer(),
                                "possibly using time/speed hack (client ping interval: " + pingInterval + "/" + CLIENT_PING_INTERVAL + "), kicking player");
                        GetConnection().Close();
                    }
                    else
                    {
                        AuditLogger.Log(GetConnection().GetActivePlayer(), "possibly using time/speed hack (client ping interval: " + pingInterval + "/" + CLIENT_PING_INTERVAL + ")");
                        GetConnection().ResetPingFailCount();
                    }
                }
            }
            else
            {
                GetConnection().ResetPingFailCount();
            }
        }
    }
}
