using System.Xml.Serialization;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine.Model;
using SM_ATTACK_STATUS = Aion.GameServer.Network.Aion.ServerPackets.SmAttackStatus;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: skillengine/effect/BleedEffect (ATracer, kecimis) : AbstractOverTimeEffect. DoT damage; super.calculate/startEffect/endEffect→base.* w/ AbnormalState.BLEED; EffectReserved.ResourceType.HP nested. Inherited Position/Hoptype/CalculateBaseValue + AttackUtil/EffectReserved/SM_ATTACK_STATUS red-tolerated.</summary>
[XmlType("BleedEffect")]
public class BleedEffect : AbstractOverTimeEffect
{
    public override void Calculate(Effect effect)
    {
        base.Calculate(effect, StatEnum.BLEED_RESISTANCE, null);
    }

    public override void StartEffect(Effect effect)
    {
        int valueWithDelta = CalculateBaseValue(effect);
        int finalDamage = AttackUtil.CalculateMagicalOverTimeSkillResult(effect, valueWithDelta, this, false);
        effect.SetReserveds(new EffectReserved(Position, finalDamage, EffectReserved.ResourceType.HP, true, false), true);
        base.StartEffect(effect, AbnormalState.BLEED);
    }

    public override void EndEffect(Effect effect)
    {
        base.EndEffect(effect, AbnormalState.BLEED);
    }

    public override void OnPeriodicAction(Effect effect)
    {
        Creature effected = effect.GetEffected();
        effected.GetController().OnAttack(effect, SmAttackStatus.TYPE.DAMAGE, effect.GetReserveds(Position).GetValue(), false, SmAttackStatus.LOG.BLEED, Hoptype);
        effected.GetObserveController().NotifyDotAttackedObservers(effect.GetEffector(), effect);
    }
}
