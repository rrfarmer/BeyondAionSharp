using Aion.GameServer.Configs.Network;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_UNK_3_5_1 (xTz). Fast-track info / server-switch response. Converges PlayerEnterWorldService. NetworkConfig/AionServerPacket red-tolerated.</summary>
public class SM_UNK_3_5_1 : AionServerPacket
{
    protected override void WriteImpl(AionConnection con)
    {
        WriteD(1);
        WriteD(0);
        WriteD(con.GetActivePlayer().GetObjectId());
        WriteD(NetworkConfig.GAMESERVER_ID);
        WriteD(0);
        WriteD(0);
    }
}
