using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_TARGET_UPDATE (Sweetkr). Sends a player's current target (objId + targetId). Player red-tolerated.</summary>
public class SM_TARGET_UPDATE : AionServerPacket
{
    private Player player;

    public SM_TARGET_UPDATE(Player player)
    {
        this.player = player;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(player.GetObjectId());
        WriteD(player.GetTarget() == null ? 0 : player.GetTarget().GetObjectId());
    }
}
