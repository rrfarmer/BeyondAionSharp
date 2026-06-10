using System;
using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Skillengine.Model;

namespace Aion.GameServer.Skillengine.Effect;

/// <summary>Java parity: skillengine/effect/HealOverTimeEffect (ATracer, kecimis) abstract : AbstractOverTimeEffect, HealEffectTemplate. ResourceType.of→EffectReserved.ResourceType.Of; switch-arrows(HealType)→switch; (Player) cast for FP/DP. HealEffectTemplate interface methods + EffectReserved/SM_ATTACK_STATUS red-tolerated.</summary>
[XmlType("HealOverTimeEffect")]
public abstract class HealOverTimeEffect : AbstractOverTimeEffect, HealEffectTemplate
{
    public override void Calculate(Effect effect)
    {
        if (!base.Calculate(effect, null, null))
            return;

        effect.AddSuccessEffect(this);
    }

    public void StartEffect(Effect effect, HealType healType)
    {
        effect.SetReserveds(new EffectReserved(position, CalculateHealValue(effect, healType), EffectReserved.ResourceType.Of(healType), false, false), true);
        base.StartEffect(effect, null);
    }

    public void OnPeriodicAction(Effect effect, HealType healType)
    {
        Creature effected = effect.GetEffected();

        int currentValue = GetCurrentStatValue(effect);
        int maxCurValue = GetMaxStatValue(effect);
        int possibleHealValue = effect.GetReserveds(position).GetValue();

        if (healType == HealType.HP && effect.GetItemTemplate() == null)
            possibleHealValue = effected.GetGameStats().GetStat(StatEnum.HEAL_SKILL_DEBOOST, possibleHealValue).GetCurrent();

        int healValue = Math.Min(maxCurValue - currentValue, possibleHealValue);

        if (healValue <= 0)
            return;

        switch (healType)
        {
            case HealType.HP:
                effected.GetLifeStats().IncreaseHp(SM_ATTACK_STATUS.TYPE.HP, healValue, effect, SM_ATTACK_STATUS.LOG.HEAL);
                break;
            case HealType.MP:
                effected.GetLifeStats().IncreaseMp(SM_ATTACK_STATUS.TYPE.MP, healValue, effect.GetSkillId(), SM_ATTACK_STATUS.LOG.MPHEAL);
                break;
            case HealType.FP:
                ((Player)effected).GetLifeStats().IncreaseFp(SM_ATTACK_STATUS.TYPE.FP, healValue, effect.GetSkillId(), SM_ATTACK_STATUS.LOG.FPHEAL);
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
