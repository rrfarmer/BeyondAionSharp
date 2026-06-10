using System;
using System.Collections.Generic;
using System.Linq;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Skillengine.Model;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_ABNORMAL_EFFECT (ATracer). Sends a creature's abnormal (buff/debuff) effects, optionally filtered to target slots. Java case 2->case 1 fall-through -> goto case; stream filter -> LINQ Where; ordinal()->(int); instanceof->is. Effect/SkillTargetSlot red-tolerated.</summary>
public class SM_ABNORMAL_EFFECT : AionServerPacket
{
    private Creature effected;
    private int effectType;
    private int abnormals;
    private ICollection<Effect> filtered;
    private int slots;

    public SM_ABNORMAL_EFFECT(Creature effected)
        : this(effected, effected.GetEffectController().GetAbnormals(), effected.GetEffectController().GetAbnormalEffects(), SkillTargetSlotExtensions.FULLSLOTS)
    {
    }

    public SM_ABNORMAL_EFFECT(Creature effected, int abnormals, ICollection<Effect> effects, int slots)
    {
        this.effected = effected;
        this.abnormals = abnormals;
        this.filtered = slots == SkillTargetSlotExtensions.FULLSLOTS ? effects
            : effects.Where(e => (slots & e.GetTargetSlot().GetId()) != 0).ToList();
        this.slots = slots;
        this.effectType = effected is Player ? 2 : 1;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(effected.GetObjectId());
        WriteC(effectType); // unk
        WriteD(0); // TODO time
        WriteD(abnormals); // unk
        WriteD(0); // unk
        WriteC(slots); // 4.5
        WriteH(filtered.Count); // effects size
        foreach (Effect effect in filtered)
        {
            switch (effectType)
            {
                case 2:
                    WriteD(effect.GetEffectorId()); // fall-through on purpose
                    goto case 1;
                case 1:
                    WriteH(effect.GetSkillId());
                    WriteC(effect.GetSkillLevel());
                    WriteC(Array.IndexOf(Enum.GetValues<SkillTargetSlot>(), effect.GetTargetSlot())); // Java ordinal() = declaration position, not the id value
                    WriteD(effect.GetRemainingTimeToDisplay());
                    break;
                default:
                    WriteH(effect.GetSkillId());
                    WriteC(effect.GetSkillLevel());
                    break;
            }
        }
    }
}
