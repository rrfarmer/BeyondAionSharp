using System.Collections.Generic;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Configs.Network;

/// <summary>Java parity: configs/network/PffConfig (Neon). @Properties keyPattern map→Dictionary (populated by config loader); getOrDefault→GetValueOrDefault. AionClientPacket red-tolerated.</summary>
public static class PffConfig
{
    /// <summary>Key: gameserver.network.pff.mode (default 1)</summary>
    public static int PFF_MODE = 1;

    /// <summary>@Properties keyPattern ^gameserver\.network\.pff\.packet\.(0[xX][0-9a-fA-F]+)$</summary>
    public static Dictionary<int, int> THRESHOLD_MILLIS_BY_PACKET_OPCODE;

    /// <returns>The allowed delay in milliseconds in which two packets of the given type may be sent from one client.</returns>
    public static int GetAllowedMillisBetweenPackets(AionClientPacket packet)
    {
        return THRESHOLD_MILLIS_BY_PACKET_OPCODE.GetValueOrDefault(packet.GetOpCode(), 0);
    }
}
