using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_MANTRA_EFFECT (Sweetkr). Plays a mantra sub-effect (effector objId + subEffectId). Creature red-tolerated.</summary>
public class SM_MANTRA_EFFECT : AionServerPacket
{
    private Creature effector;
    private int subEffectId;

    public SM_MANTRA_EFFECT(Creature effector, int subEffectId)
    {
        this.effector = effector;
        this.subEffectId = subEffectId;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(0x00);// unk
        WriteD(effector.GetObjectId());
        WriteH(subEffectId);
    }
}
