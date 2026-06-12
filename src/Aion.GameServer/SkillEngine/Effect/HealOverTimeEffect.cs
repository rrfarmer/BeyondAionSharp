using System;
using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine.Model;
using SM_ATTACK_STATUS = Aion.GameServer.Network.Aion.ServerPackets.SmAttackStatus;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: skillengine/effect/HealOverTimeEffect (ATracer, kecimis) abstract : AbstractOverTimeEffect, HealEffectTemplate. ResourceType.of→EffectReserved.ResourceType.Of; switch-arrows(HealType)→switch; (Player) cast for FP/DP. HealEffectTemplate interface methods + EffectReserved/SM_ATTACK_STATUS red-tolerated.</summary>
[XmlType("HealOverTimeEffect")]
public abstract class HealOverTimeEffect : AbstractOverTimeEffect, HealEffectTemplate
{
    // Java parity: HealEffectTemplate interface methods, implemented by the concrete heal effects.
    public abstract int GetCurrentStatValue(Effect effect);

    public abstract int GetMaxStatValue(Effect effect);

    public override void Calculate(Effect effect)
    {
        if (!base.Calculate(effect, null, null))
            return;

        effect.AddSuccessEffect(this);
    }

    public void StartEffect(Effect effect, HealType healType)
    {
        effect.SetReserveds(new EffectReserved(Position, ((HealEffectTemplate)this).CalculateHealValue(effect, healType), EffectReservedResourceTypeExtensions.Of(healType), false, false), true);
        base.StartEffect(effect, null);
    }

    public void OnPeriodicAction(Effect effect, HealType healType)
    {
        Creature effected = effect.GetEffected();

        int currentValue = GetCurrentStatValue(effect);
        int maxCurValue = GetMaxStatValue(effect);
        int possibleHealValue = effect.GetReserveds(Position).GetValue();

        if (healType == HealType.HP && effect.GetItemTemplate() == null)
            possibleHealValue = effected.GetGameStats().GetStat(StatEnum.HEAL_SKILL_DEBOOST, possibleHealValue).GetCurrent();

        int healValue = Math.Min(maxCurValue - currentValue, possibleHealValue);

        if (healValue <= 0)
            return;

        switch (healType)
        {
            case HealType.HP:
                effected.GetLifeStats().IncreaseHp(SmAttackStatus.TYPE.HP, healValue, effect, SmAttackStatus.LOG.HEAL);
                break;
            case HealType.MP:
                effected.GetLifeStats().IncreaseMp(SmAttackStatus.TYPE.MP, healValue, effect.GetSkillId(), SmAttackStatus.LOG.MPHEAL);
                break;
            case HealType.FP:
                ((Player)effected).GetLifeStats().IncreaseFp(SmAttackStatus.TYPE.FP, healValue, effect.GetSkillId(), SmAttackStatus.LOG.FPHEAL);
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

    public bool AllowHpHealBoost(Effect effect)
    {
        return !percent && effect.GetItemTemplate() == null;
    }

    public bool AllowHpHealSkillDeboost(Effect effect)
    {
        return false; // calculated in onPeriodicAction instead
    }

    public int CalculateBaseHealValue(Effect effect)
    {
        return CalculateBaseValue(effect);
    }
}
