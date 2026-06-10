using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_TARGET_SELECTED (Sweetkr, -Enomine-). Sends the selected target's id, level, and HP/MP (current+max) for creatures. instanceof Creature->is Creature creature. VisibleObject/Creature/AionServerPacket red-tolerated.</summary>
public class SM_TARGET_SELECTED : AionServerPacket
{
    private int targetObjId;
    private int level;
    private int maxHp, currentHp;
    private int maxMp, currentMp;

    public SM_TARGET_SELECTED(VisibleObject target)
    {
        if (target != null)
        {
            this.targetObjId = target.GetObjectId();
            if (target is Creature)
            {
                Creature creature = (Creature)target;
                this.level = creature.GetLevel();
                this.maxHp = creature.GetLifeStats().GetMaxHp();
                this.currentHp = creature.GetLifeStats().GetCurrentHp();
                this.maxMp = creature.GetLifeStats().GetMaxMp();
                this.currentMp = creature.GetLifeStats().GetCurrentMp();
            }
        }
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(targetObjId);
        WriteH(level);
        WriteD(maxHp);
        WriteD(currentHp);
        WriteD(maxMp);// new 4.0
        WriteD(currentMp);// new 4.0
    }
}
