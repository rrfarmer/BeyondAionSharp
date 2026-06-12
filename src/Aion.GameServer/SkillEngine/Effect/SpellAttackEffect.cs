using System.Xml.Serialization;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine.Model;
using SM_ATTACK_STATUS = Aion.GameServer.Network.Aion.ServerPackets.SmAttackStatus;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: skillengine/effect/SpellAttackEffect (kecimis) : AbstractOverTimeEffect. DoT magical damage; useMagicBoost excludes skill 21110. Inherited Position/Hoptype/CalculateBaseValue + AttackUtil/EffectReserved/SM_ATTACK_STATUS red-tolerated.</summary>
[XmlType("SpellAttackEffect")]
public class SpellAttackEffect : AbstractOverTimeEffect
{
    public override void StartEffect(Effect effect)
    {
        int valueWithDelta = CalculateBaseValue(effect);
        int finalDamage = AttackUtil.CalculateMagicalOverTimeSkillResult(effect, valueWithDelta, this, UseMagicBoost(effect));
        effect.SetReserveds(new EffectReserved(Position, finalDamage, EffectReserved.ResourceType.HP, true, false), true);
        base.StartEffect(effect);
    }

    public override void OnPeriodicAction(Effect effect)
    {
        Creature effected = effect.GetEffected();
        effected.GetController().OnAttack(effect, SmAttackStatus.TYPE.DAMAGE, effect.GetReserveds(Position).GetValue(), false, SmAttackStatus.LOG.SPELLATK, Hoptype);
        effected.GetObserveController().NotifyDotAttackedObservers(effect.GetEffector(), effect);
    }

    /// <summary>Retail templates exclude apply_*_boost for some skills; if parsing succeeds, this can be removed.</summary>
    private bool UseMagicBoost(Effect effect)
    {
        return effect.GetSkillId() != 21110; // Shugo Venom
    }
}
