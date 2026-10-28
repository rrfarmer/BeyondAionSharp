using System.Xml.Serialization;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine.Model;
using SM_ATTACK_STATUS = Aion.GameServer.Network.Aion.ServerPackets.SmAttackStatus;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: skillengine/effect/SpellAtkDrainEffect (Sippolo, kecimis) : AbstractOverTimeEffect. @XmlAttribute(name=)→[XmlAttribute(...)]; drains hp/mp percent of damage to effector. Inherited position/Hoptype/CalculateBaseValue + AttackUtil/SM_ATTACK_STATUS red-tolerated.</summary>
[XmlType("SpellAtkDrainEffect")]
public class SpellAtkDrainEffect : AbstractOverTimeEffect
{
    [XmlAttribute("hp_percent")]
    public int hpPercent;
    [XmlAttribute("mp_percent")]
    public int mpPercent;

    protected override void ResolveMagicalCritical(Effect effect)
    {
        if (RetailNpcStatSpellDrain.TryGetPreparedNpcPeriodicProgram(effect,out _))
        {
            var program=RetailNpcStatSpellDrain.RequireNpcPeriodicProgram(effect,this);
            if (!RetailNpcStatPeriodicCombat.TryResolveCritical(effect,this)
                || !RetailNpcStatPeriodicCombat.MatchesPreparedNpcDrainSource(effect,this,program.Operands!))
                throw new InvalidOperationException("Selected NPC periodic drain source unavailable");
            return;
        }
        effect.RollMagicalCritical(Position, CalculateCritProbMod(effect)); // periodic damage ignores the apply_magical_critical flag
    }

    public override void StartEffect(Effect effect)
    {
        if (RetailNpcStatSpellDrain.TryGetPreparedNpcPeriodicProgram(effect,out _))
        {
            var program=RetailNpcStatSpellDrain.RequireNpcPeriodicProgram(effect,this);
            if (!RetailNpcStatPeriodicCombat.MatchesPreparedNpcDrainSource(effect,this,program.Operands!)
                || !RetailNpcStatPeriodicCombat.TryStart(effect,this))
                throw new InvalidOperationException("Selected NPC periodic drain was not calculated");
        }
        base.StartEffect(effect);
    }

    public override void OnPeriodicAction(Effect effect)
    {
        if (RetailNpcStatSpellDrain.TryGetPreparedNpcPeriodicProgram(effect,out _))
        {
            var program=RetailNpcStatSpellDrain.RequireNpcPeriodicProgram(effect,this);
            if (!RetailNpcStatPeriodicCombat.MatchesPreparedNpcDrainSource(effect,this,program.Operands!)
                || !RetailNpcStatPeriodicCombat.TryTick(effect,this,out int? loss))
                throw new InvalidOperationException("Selected NPC periodic drain tick unavailable");
            var caster=(Npc)effect.GetEffector();
            var target=(Npc)effect.GetEffected();
            RetailNpcStatSpellDrain.ApplyNpcPeriodicHpHeal(effect,caster,target,program.Operands!,program.SourceLevel,loss,
                ()=>caster.GetController().RetailLifeEpoch==program.CasterLife && program.IsNativeEligible!(caster));
            return;
        }
        int valueWithDelta = CalculateBaseValue(effect);
        int damage = AttackUtil.CalculateMagicalOverTimeSkillResult(effect, valueWithDelta, this, effect.GetSkillTemplate().IsApplyMagicalSkillBoostBonus());
        effect.GetEffected().GetController().OnAttack(effect, SmAttackStatus.TYPE.DAMAGE, damage, true, SmAttackStatus.LOG.SPELLATKDRAIN, Hoptype, effect.IsMagicalCritical(Position));
        effect.GetEffector().GetObserveController().NotifyAttackObservers(effect.GetEffected(), effect.GetSkillId());

        // Drain (heal) portion of damage inflicted
        if (hpPercent != 0)
        {
            effect.GetEffector().GetLifeStats().IncreaseHp(SmAttackStatus.TYPE.HP, damage * hpPercent / 100, effect, SmAttackStatus.LOG.SPELLATKDRAIN);
        }
        if (mpPercent != 0)
        {
            effect.GetEffector().GetLifeStats().IncreaseMp(SmAttackStatus.TYPE.MP, damage * mpPercent / 100, effect.GetSkillId(), SmAttackStatus.LOG.SPELLATKDRAIN);
        }
    }
}
