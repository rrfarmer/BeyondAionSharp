using System;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;
using static Aion.GameServer.Network.Aion.Serverpackets.SM_ATTACK_STATUS;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: skillengine/effect/SpellAtkDrainInstantEffect (ATracer) : DamageEffect. @XmlAttribute(name="hp_percent"/"mp_percent"); super.applyEffect then schedule(1000ms): hp→increaseHp(HP, reserveds*hpPercent/100, effect, LOG.SPELLATKDRAININSTANT), mp→increaseMp(ABSORBED_MP, ...*mpPercent/100, skillId, ...). anonymous Runnable→async delegate. Effect red-tolerated.</summary>
[XmlType("SpellAtkDrainInstantEffect")]
public class SpellAtkDrainInstantEffect : DamageEffect
{
    [XmlAttribute("hp_percent")]
    private int hpPercent;
    [XmlAttribute("mp_percent")]
    private int mpPercent;

    public override void ApplyEffect(Effect effect)
    {
        base.ApplyEffect(effect);
        ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            if (hpPercent != 0)
            {
                effect.GetEffector().GetLifeStats().IncreaseHp(TYPE.HP, effect.GetReserveds(position).GetValue() * hpPercent / 100, effect,
                        LOG.SPELLATKDRAININSTANT);
            }
            if (mpPercent != 0)
            {
                effect.GetEffector().GetLifeStats().IncreaseMp(TYPE.ABSORBED_MP, effect.GetReserveds(position).GetValue() * mpPercent / 100,
                        effect.GetSkillId(), LOG.SPELLATKDRAININSTANT);
            }
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(1000)); // on retail the effect is applied about 1sec later (maybe based on animationTime/hitTime?)
    }
}
