using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_PLAYER_STATE (Luno, Sweetkr). Sends a creature's visual/see state (e.g. stop login blinking). Creature/CreatureVisualState red-tolerated.</summary>
public class SM_PLAYER_STATE : AionServerPacket
{
    private int playerObjId;
    private int visualState;
    private int seeState;

    public SM_PLAYER_STATE(Creature creature)
    {
        this.playerObjId = creature.GetObjectId();
        this.visualState = creature.GetVisualState();
        this.seeState = creature.GetSeeState();
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(playerObjId);
        WriteC(visualState);
        WriteC(seeState);
        WriteC(visualState == CreatureVisualState.BLINKING.GetId() ? 0x01 : 0x00);
    }
}
