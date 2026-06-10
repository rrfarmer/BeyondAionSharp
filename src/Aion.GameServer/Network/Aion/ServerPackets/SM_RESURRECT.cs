using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_RESURRECT (ATracer, Jego). Resurrect prompt (creature name + skillId). Creature red-tolerated.</summary>
public class SM_RESURRECT : AionServerPacket
{
    private string name;
    private int skillId;

    public SM_RESURRECT(Creature creature)
        : this(creature, 0)
    {
    }

    public SM_RESURRECT(Creature creature, int skillId)
    {
        this.name = creature.GetName();
        this.skillId = skillId;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteS(name);
        WriteH(skillId); // unk
        WriteD(0);
    }
}
