using System;
using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine.Model;
using SM_ATTACK_STATUS = Aion.GameServer.Network.Aion.ServerPackets.SmAttackStatus;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: skillengine/effect/AbstractHealEffect (ATracer, Wakizashi, kecimis) abstract : EffectTemplate, HealEffectTemplate. calculate/applyEffect take HealType; instanceof Proc*HealInstantEffect→is; switch HP/MP/FP/DP; HealEffectTemplate.super.calculateHealValue→HealEffectTemplate.CalculateHealValueDefault(this,...); ResourceType.of→EffectReserved.ResourceType.Of. GetCurrentStatValue/GetMaxStatValue left abstract. EffectTemplate/EffectReserved/Proc effects red-tolerated.</summary>
[XmlType("AbstractHealEffect")]
public abstract class AbstractHealEffect : EffectTemplate, HealEffectTemplate
{
    [XmlAttribute]
    protected bool percent;

    public void Calculate(Effect effect, HealType healType)
    {
        if (!base.Calculate(effect, null, null))
            return;
        effect.SetReserveds(new EffectReserved(Position, CalculateHealValue(effect, healType), EffectReservedResourceTypeExtensions.Of(healType), false), false);
    }

    public void ApplyEffect(Effect effect, HealType healType)
    {
        Creature effected = effect.GetEffected();
        int healValue = effect.GetReserveds(Position).GetValue();
        switch (healType)
        {
            case HealType.HP:
                if (this is ProcHealInstantEffect) // item heal, eg potions
                    effected.GetLifeStats().IncreaseHp(SmAttackStatus.TYPE.HP, healValue, effect.GetEffector());
                else
                    effected.GetLifeStats().IncreaseHp(SmAttackStatus.TYPE.REGULAR, healValue, effect.GetEffector());
                break;
            case HealType.MP:
                if (this is ProcMPHealInstantEffect) // item heal, eg potions
                    effected.GetLifeStats().IncreaseMp(SmAttackStatus.TYPE.MP, healValue, 0, SmAttackStatus.LOG.REGULAR);
                else
                    effected.GetLifeStats().IncreaseMp(SmAttackStatus.TYPE.HEAL_MP, healValue, 0, SmAttackStatus.LOG.REGULAR);
                break;
            case HealType.FP:
                if (!(effected is Player))
                    return;
                ((Player)effected).GetLifeStats().IncreaseFp(SmAttackStatus.TYPE.FP_RINGS, healValue, 0, SmAttackStatus.LOG.REGULAR);
                break;
            case HealType.DP:
                ((Player)effected).GetCommonData().AddDp(healValue);
                break;
        }
    }

    public bool IsPercent()
    {
        return percent;
    }

    public virtual bool AllowHpHealBoost(Effect effect)
    {
        return !percent;
    }

    public virtual bool AllowHpHealSkillDeboost(Effect effect)
    {
        return true;
    }

    public int CalculateBaseHealValue(Effect effect)
    {
        return CalculateBaseValue(effect);
    }

    public int CalculateHealValue(Effect effect, HealType type)
    {
        if (type == HealType.HP && effect.GetEffected().GetEffectController().IsAbnormalSet(AbnormalState.DISEASE))
            return 0;
        int cap = GetMaxStatValue(effect) - GetCurrentStatValue(effect);
        int healValue = HealEffectTemplate.CalculateHealValueDefault(this, effect, type);
        return Math.Min(cap, healValue);
    }

    public abstract int GetCurrentStatValue(Effect effect);
    public abstract int GetMaxStatValue(Effect effect);
}
