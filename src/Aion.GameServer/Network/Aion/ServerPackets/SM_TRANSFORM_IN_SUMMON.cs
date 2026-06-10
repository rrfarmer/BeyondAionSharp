using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_TRANSFORM_IN_SUMMON (xTz). Sends a summon-transform link (summon objId + owner name + owner objId). Creature/Player red-tolerated.</summary>
public class SM_TRANSFORM_IN_SUMMON : AionServerPacket
{
    private Player player;
    private int summonObject;

    public SM_TRANSFORM_IN_SUMMON(Player player, Creature creature)
        : this(player, creature.GetObjectId())
    {
    }

    public SM_TRANSFORM_IN_SUMMON(Player player, int creatureObjectId)
    {
        this.player = player;
        this.summonObject = creatureObjectId;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(summonObject);
        WriteS(player.GetName());
        WriteD(player.GetObjectId());
    }
}
