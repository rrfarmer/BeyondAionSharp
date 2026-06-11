using System.Xml.Serialization;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Skillengine.Model;

namespace Aion.GameServer.Skillengine.Effects;

/// <summary>Java parity: skillengine/effect/NoReduceSpellATKInstantEffect (Sippolo) : DamageEffect. @XmlAttribute percent; @XmlAttribute(name="max_damage")→[XmlAttribute("max_damage")]; calculateDamage override: percent→maxHp*pct, max_damage cap, AttackUtil.calculateSkillResult(effect, value, this, false); shouldApplyAttackerMovementModifier→false. AttackUtil/Effect red-tolerated.</summary>
[XmlType("NoReduceSpellATKInstantEffect")]
public class NoReduceSpellATKInstantEffect : DamageEffect
{
    [XmlAttribute]
    protected bool percent;
    [XmlAttribute("max_damage")]
    protected int max_damage;

    public override void CalculateDamage(Effect effect)
    {
        int valueWithDelta = CalculateBaseValue(effect);
        if (percent)
        {
            float percentToCount = valueWithDelta / 100f;
            valueWithDelta = (int)(effect.GetEffected().GetLifeStats().GetMaxHp() * percentToCount);
        }

        if (max_damage > 0)
            valueWithDelta = valueWithDelta > max_damage ? max_damage : valueWithDelta;

        AttackUtil.CalculateSkillResult(effect, valueWithDelta, this, false);
    }

    public override bool ShouldApplyAttackerMovementModifier()
    {
        return false;
    }
}
