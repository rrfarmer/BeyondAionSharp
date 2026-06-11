using System;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;
using static Aion.GameServer.Network.Aion.Serverpackets.SM_ATTACK_STATUS;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: skillengine/effect/DelayedSpellAttackInstantEffect (ATracer) : DamageEffect. @XmlAttribute delay; applyEffect: base value, element!=NONE→*knowledge/100 (int division per Java); calculateSkillResult ignoreShields=true; finalPosition=this.position; anonymous Runnable→async delegate at delay ms: onAttack(DELAYDAMAGE, reserveds(finalPosition), true, LOG.DELAYEDSPELLATKINSTANT, hopType) + notifyAttackObservers; calculateDamage empty. AttackUtil/Effect red-tolerated.</summary>
[XmlType("DelayedSpellAttackInstantEffect")]
public class DelayedSpellAttackInstantEffect : DamageEffect
{
    [XmlAttribute]
    protected int delay;

    public override void ApplyEffect(Effect effect)
    {
        int valueWithDelta = CalculateBaseValue(effect);
        if (element != SkillElement.NONE)
            valueWithDelta *= effect.GetEffector().GetGameStats().GetKnowledge().GetCurrent() / 100;

        AttackUtil.CalculateSkillResult(effect, valueWithDelta, this, true); // ignores shields on retail
        int finalPosition = this.position;
        ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            effect.GetEffected().GetController().OnAttack(effect, TYPE.DELAYDAMAGE, effect.GetReserveds(finalPosition).GetValue(), true,
                LOG.DELAYEDSPELLATKINSTANT, hopType);
            effect.GetEffector().GetObserveController().NotifyAttackObservers(effect.GetEffected(), effect.GetSkillId());
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(delay));
    }

    public override void CalculateDamage(Effect effect)
    {
    }
}
