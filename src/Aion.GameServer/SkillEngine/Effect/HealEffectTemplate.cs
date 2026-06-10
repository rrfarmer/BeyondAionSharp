using System;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Skillengine.Model;

namespace Aion.GameServer.Skillengine.Effect;

/// <summary>Java parity: skillengine/effect/HealEffectTemplate (Neon) interface w/ default calculateHealValue. C# default interface method delegates to a static helper so overriding classes can invoke the "interface default" (Java's HealEffectTemplate.super.calculateHealValue → CalculateHealValueDefault). Math.round(float)→(int)Math.Floor(+0.5f). Effect/StatEnum red-tolerated.</summary>
public interface HealEffectTemplate
{
    bool IsPercent();
    bool AllowHpHealBoost(Effect effect);
    bool AllowHpHealSkillDeboost(Effect effect);
    int GetCurrentStatValue(Effect effect);
    int GetMaxStatValue(Effect effect);
    int CalculateBaseHealValue(Effect effect);

    int CalculateHealValue(Effect effect, HealType type) => CalculateHealValueDefault(this, effect, type);

    // Java default-method body extracted to a static so AbstractHealEffect can invoke it (Java: HealEffectTemplate.super.calculateHealValue).
    static int CalculateHealValueDefault(HealEffectTemplate self, Effect effect, HealType type)
    {
        int healValue = self.IsPercent() ? self.GetMaxStatValue(effect) * self.CalculateBaseHealValue(effect) / 100 : self.CalculateBaseHealValue(effect);

        if (type == HealType.HP && healValue >= 0) // ignore skills like Spirit Absorption that apply damage via negative heals
        {
            if (self.AllowHpHealBoost(effect))
            {
                // caster's heal boost from equipment, titles, etc. (capped at 1000 / 100% boost)
                int healBoost = effect.GetEffector().GetGameStats().GetStat(StatEnum.HEAL_BOOST, 0).GetCurrent();
                // caster's heal related effects (passive boosts, active buffs e.g. blessed shield)
                int healSkillBoost = effect.GetEffector().GetGameStats().GetStat(StatEnum.HEAL_SKILL_BOOST, 1000).GetCurrent() - 1000;
                healValue += (int)Math.Floor(healValue * Math.Min(2000, healBoost + healSkillBoost) / 1000f + 0.5f);
            }
            // apply target's heal related effects (e.g. brilliant protection)
            if (self.AllowHpHealSkillDeboost(effect))
                healValue = Math.Max(0, effect.GetEffected().GetGameStats().GetStat(StatEnum.HEAL_SKILL_DEBOOST, healValue).GetCurrent());
        }
        return healValue;
    }
}
