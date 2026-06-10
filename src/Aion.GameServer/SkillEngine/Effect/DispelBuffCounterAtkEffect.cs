using System.Xml.Serialization;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Skillengine.Model;

namespace Aion.GameServer.Skillengine.Effect;

/// <summary>Java parity: skillengine/effect/DispelBuffCounterAtkEffect : DamageEffect. @XmlAttribute dpower/power/hitvalue/hitdelta; @XmlAttribute(name="dispel_level"); getCritProbMod2→0 (cannot crit, base virtual via converge); applyEffect→dispelBuffCounterAtkEffect; calculateDamage: count=base, finalPower, calculateBuffsOrEffectorDebuffsToRemove, valueWithDelta formula, calculateSkillResult; shouldApplyAttackerMovementModifier→false; endEffect→resetDesignatedDispelEffect+super. AttackUtil/Creature red-tolerated.</summary>
[XmlType("DispelBuffCounterAtkEffect")]
public class DispelBuffCounterAtkEffect : DamageEffect
{
    [XmlAttribute]
    private int dpower;
    [XmlAttribute]
    private int power;
    [XmlAttribute]
    private int hitvalue;
    [XmlAttribute]
    private int hitdelta;
    [XmlAttribute("dispel_level")]
    private int dispelLevel;

    public override int GetCritProbMod2()
    {
        return 0; // critProbMod2 is 100 by default but this effect type cannot crit
    }

    public override void ApplyEffect(Effect effect)
    {
        base.ApplyEffect(effect);
        effect.GetEffected().GetEffectController().DispelBuffCounterAtkEffect(effect);
    }

    public override void CalculateDamage(Effect effect)
    {
        Creature effected = effect.GetEffected();
        int count = CalculateBaseValue(effect);
        int finalPower = power + dpower * effect.GetSkillLevel();

        int dispelledEffectCount = effected.GetEffectController().CalculateBuffsOrEffectorDebuffsToRemove(effect, count, dispelLevel, finalPower);
        int valueWithDelta = dispelledEffectCount > 0 ? hitvalue + ((hitvalue / 2) * (dispelledEffectCount - 1)) + hitdelta * effect.GetSkillLevel() : 0;
        AttackUtil.CalculateSkillResult(effect, valueWithDelta, this, false);
    }

    public override bool ShouldApplyAttackerMovementModifier()
    {
        return false;
    }

    public override void EndEffect(Effect effect)
    {
        effect.GetEffected().GetEffectController().ResetDesignatedDispelEffect(effect);
        base.EndEffect(effect);
    }
}
