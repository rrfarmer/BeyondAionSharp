using System;
using System.Diagnostics;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_TIME_CHECK (-Nemesiss-). Server uptime + client nanoTime echo. ManagementFactory.getRuntimeMXBean().getUptime() (JVM uptime ms) -> process uptime via Process.StartTime.</summary>
public class SM_TIME_CHECK : AionServerPacket
{
    private int serverUpTime, nanoTime;

    public SM_TIME_CHECK(int nanoTime)
    {
        this.serverUpTime = (int)(DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime()).TotalMilliseconds;
        this.nanoTime = nanoTime;
    }

    // Java parity (writeImpl audited 1:1 vs game-server/src/com/aionemu/gameserver/network/aion/serverpackets/SM_TIME_CHECK.java): 2026-06-17
    protected override void WriteImpl(AionConnection con)
    {
        WriteD(serverUpTime);
        WriteD(nanoTime);
    }
}
