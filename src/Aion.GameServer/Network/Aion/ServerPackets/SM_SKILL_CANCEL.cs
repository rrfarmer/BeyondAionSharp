using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_SKILL_CANCEL (Sweetkr). Cancels a creature's skill (objId + skillId). Creature red-tolerated.</summary>
public class SM_SKILL_CANCEL : AionServerPacket
{
    private Creature creature;
    private int skillId;

    public SM_SKILL_CANCEL(Creature creature, int skillId)
    {
        this.creature = creature;
        this.skillId = skillId;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(creature.GetObjectId());
        WriteH(skillId);
    }
}
