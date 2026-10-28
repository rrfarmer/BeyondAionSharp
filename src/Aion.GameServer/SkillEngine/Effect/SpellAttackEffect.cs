using Aion.GameServer.Controllers.Attack.RetailNpc;
using System.Xml.Serialization;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine.Model;
using SM_ATTACK_STATUS = Aion.GameServer.Network.Aion.ServerPackets.SmAttackStatus;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: skillengine/effect/SpellAttackEffect (kecimis) : AbstractOverTimeEffect. DoT magical damage; magic boost follows the skill's apply_magical_skill_boost_bonus. Inherited Position/Hoptype/CalculateBaseValue + AttackUtil/EffectReserved/SM_ATTACK_STATUS red-tolerated.</summary>
[XmlType("SpellAttackEffect")]
public class SpellAttackEffect : AbstractOverTimeEffect
{
    public override void Calculate(Effect effect)
    {
        if(RetailNpcStatSpellDrain.TryCalculatePeriodicSpell(effect,this))return;
        base.Calculate(effect);
    }

    protected override void ResolveMagicalCritical(Effect effect)
    {
        if(RetailNpcStatPeriodicCombat.TryResolveCritical(effect,this))return;
        effect.RollMagicalCritical(Position, CalculateCritProbMod(effect)); // periodic damage ignores the apply_magical_critical flag
    }

    public override void StartEffect(Effect effect)
    {
        if(SkyTempleNpcPeriodicEffects.TryStart(effect,this,null))return;
        if(RetailNpcStatPeriodicCombat.TryStart(effect,this)){base.StartEffect(effect);return;}
        int valueWithDelta = CalculateBaseValue(effect);
        int finalDamage = AttackUtil.CalculateMagicalOverTimeSkillResult(effect, valueWithDelta, this, effect.GetSkillTemplate().IsApplyMagicalSkillBoostBonus());
        effect.SetReserveds(new EffectReserved(Position, finalDamage, EffectReserved.ResourceType.HP, true, false), true);
        base.StartEffect(effect);
    }

    public override void OnPeriodicAction(Effect effect)
    {
        if(RetailNpcStatPeriodicCombat.TryTick(effect,this))return;
        Creature effected = effect.GetEffected();
        effected.GetController().OnAttack(effect, SmAttackStatus.TYPE.DAMAGE, effect.GetReserveds(Position).GetValue(), false, SmAttackStatus.LOG.SPELLATK, Hoptype,
            effect.IsMagicalCritical(Position));
        effected.GetObserveController().NotifyDotAttackedObservers(effect.GetEffector(), effect);
    }
}
