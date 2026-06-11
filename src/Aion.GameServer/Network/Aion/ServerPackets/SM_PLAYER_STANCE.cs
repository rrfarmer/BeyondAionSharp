using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_PLAYER_STANCE (prix). Sends a player's stance state (objId + state). Player red-tolerated.</summary>
public class SM_PLAYER_STANCE : AionServerPacket
{
    private Player player;
    private int state;

    public SM_PLAYER_STANCE(Player player, int state)
    {
        this.player = player;
        this.state = state; // 0 = off, 1 = block, flight, glide, jump, etc.
        // 2 = stationary object
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(player.GetObjectId());
        WriteC(state);
    }
}
