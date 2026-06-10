using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_GM_SEARCH. Sends a GM player-search result string (name + world + coords). Player red-tolerated.</summary>
public class SM_GM_SEARCH : AionServerPacket
{
    private Player player;

    public SM_GM_SEARCH(Player player)
    {
        this.player = player;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteS("search " + player.GetName() + " " + player.GetWorldId() + " " + (int)player.GetX() + " " + (int)player.GetY() + " " + (int)player.GetZ());
    }
}
