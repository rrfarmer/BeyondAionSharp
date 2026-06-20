using System;
using System.Collections.Generic;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_ABNORMAL_STATE (Avol, ATracer). Sends a creature's active abnormal effect icons (effector/skill/level/slot/remaining time) + abnormal bitmask. Converges PlayerEffectController. Collection->ICollection; getTargetSlot().ordinal()->Array.IndexOf(Enum.GetValues<SkillTargetSlot>()) (ordinal = declaration position, NOT the id value: BUFF=1/DEBUFF=2/... but ordinals 0/1/...). Effect/AionServerPacket red-tolerated.</summary>
public class SM_ABNORMAL_STATE : AionServerPacket
{
    private ICollection<Effect> effects;
    private int abnormals;
    private int slot;

    public SM_ABNORMAL_STATE(ICollection<Effect> effects, int abnormals, int slot)
    {
        this.effects = effects;
        this.abnormals = abnormals;
        this.slot = slot;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(abnormals);
        WriteD(0);
        WriteD(0); // 4.5
        WriteC(slot);
        WriteH(effects.Count);

        foreach (Effect effect in effects)
        {
            WriteD(effect.GetEffectorId());
            WriteH(effect.GetSkillId());
            WriteC(effect.GetSkillLevel());
            WriteC(Array.IndexOf(Enum.GetValues<SkillTargetSlot>(), effect.GetTargetSlot())); // Java ordinal() = declaration position, not the id value
            WriteD(effect.GetRemainingTimeToDisplay());
        }
    }
}
