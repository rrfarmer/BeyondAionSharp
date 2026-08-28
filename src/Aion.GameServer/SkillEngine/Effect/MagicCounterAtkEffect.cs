using System;
using System.Xml.Serialization;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.SkillEngine.Model;
using static Aion.GameServer.Network.Aion.ServerPackets.SmAttackStatus;
using static Aion.GameServer.SkillEngine.Model.Skill;
using SM_ATTACK_STATUS = Aion.GameServer.Network.Aion.ServerPackets.SmAttackStatus;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: skillengine/effect/MagicCounterAtkEffect (ViAl) : EffectTemplate. @XmlAttribute maxdmg; applyEffect→addToEffectedController; startEffect: anonymous ActionObserver(ENDSKILLCAST).endSkillCast→nested CounterObserver capturing outer+effect+effected: non-ITEM && MAGICAL→maxHpDamage=maxHp.base*calculateBaseValue/100f, pvp/pve-adjusted via StatFunctions, damage=min(maxdmg, adjusted), onAttack(MAGICCOUNTERATK, ..., Hoptype). Skill.SkillMethod/SkillType red-tolerated.</summary>
[XmlType("MagicCounterAtkEffect")]
public class MagicCounterAtkEffect : EffectTemplate
{
    [XmlAttribute]
    public int maxdmg;

    public override void ApplyEffect(Effect effect)
    {
        effect.AddToEffectedController();
    }

    public override void StartEffect(Effect effect)
    {
        Creature effected = effect.GetEffected();
        effect.AddObserver(effected, new CounterObserver(this, effect, effected));
    }

    private sealed class CounterObserver : ActionObserver
    {
        private readonly MagicCounterAtkEffect outer;
        private readonly Effect effect;
        private readonly Creature effected;

        public CounterObserver(MagicCounterAtkEffect outer, Effect effect, Creature effected)
            : base(ObserverType.ENDSKILLCAST)
        {
            this.outer = outer;
            this.effect = effect;
            this.effected = effected;
        }

        public override void EndSkillCast(Skill skill)
        {
            if (skill.GetSkillMethod() != SkillMethod.ITEM && skill.GetSkillTemplate().GetType_() == SkillType.MAGICAL)
            {
                float maxHpDamage = effected.GetGameStats().GetMaxHp().GetBase() * outer.CalculateBaseValue(effect) / 100f;

                float adjustedDamage = Aion.GameServer.Utils.Stats.StatFunctions.AdjustDamageByPvpOrPveModifiers(
                    effect.GetEffector(), effect.GetEffected(), maxHpDamage, effect.GetSkillTemplate().GetPvpDamage(), false, outer.Element);

                int finalDamage = (int)Math.Min(outer.maxdmg, adjustedDamage);

                effected.GetController().OnAttack(effect, TYPE.MAGICCOUNTERATK, finalDamage, true, LOG.MAGICCOUNTERATK, outer.Hoptype);
            }
        }
    }
}
