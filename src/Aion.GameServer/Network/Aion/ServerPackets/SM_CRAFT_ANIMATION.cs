using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_CRAFT_ANIMATION (Mr. Poke). Crafting animation (player/target objIds + skillId + action).</summary>
public class SM_CRAFT_ANIMATION : AionServerPacket
{
    private int playerObjId;
    private int targetObjectId;
    private int skillId;
    private int action;

    public SM_CRAFT_ANIMATION(int playerObjId, int targetObjectId, int skillId, int action)
    {
        this.playerObjId = playerObjId;
        this.targetObjectId = targetObjectId;
        this.skillId = skillId;
        this.action = action;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(playerObjId);
        WriteD(targetObjectId);
        WriteH(skillId);
        WriteC(action);
    }
}
