using System.Xml.Serialization;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Skillengine.Model;

namespace Aion.GameServer.Skillengine.Effect;

/// <summary>Java parity: skillengine/effect/SpellAttackEffect (kecimis) : AbstractOverTimeEffect. DoT magical damage; useMagicBoost excludes skill 21110. Inherited position/hopType/CalculateBaseValue + AttackUtil/EffectReserved/SM_ATTACK_STATUS red-tolerated.</summary>
[XmlType("SpellAttackEffect")]
public class SpellAttackEffect : AbstractOverTimeEffect
{
    public override void StartEffect(Effect effect)
    {
        int valueWithDelta = CalculateBaseValue(effect);
        int finalDamage = AttackUtil.CalculateMagicalOverTimeSkillResult(effect, valueWithDelta, this, UseMagicBoost(effect));
        effect.SetReserveds(new EffectReserved(position, finalDamage, EffectReserved.ResourceType.HP, true, false), true);
        base.StartEffect(effect);
    }

    public override void OnPeriodicAction(Effect effect)
    {
        Creature effected = effect.GetEffected();
        effected.GetController().OnAttack(effect, SM_ATTACK_STATUS.TYPE.DAMAGE, effect.GetReserveds(position).GetValue(), false, SM_ATTACK_STATUS.LOG.SPELLATK, hopType);
        effected.GetObserveController().NotifyDotAttackedObservers(effect.GetEffector(), effect);
    }

    /// <summary>Retail templates exclude apply_*_boost for some skills; if parsing succeeds, this can be removed.</summary>
    private bool UseMagicBoost(Effect effect)
    {
        return effect.GetSkillId() != 21110; // Shugo Venom
    }
}
