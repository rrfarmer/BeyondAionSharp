using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_GAME_TIME (Ben). Sends current server game time in minutes since 1/1/00 00:00:00. Converges PlayerEnterWorldService. GameTimeService/AionServerPacket red-tolerated.</summary>
public class SM_GAME_TIME : AionServerPacket
{
    protected override void WriteImpl(AionConnection con)
    {
        WriteD(GameTimeService.GetInstance().GetGameTime().GetTime()); // Minutes since 1/1/00 00:00:00
    }
}
