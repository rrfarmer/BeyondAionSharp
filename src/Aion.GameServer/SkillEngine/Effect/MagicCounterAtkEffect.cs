using System;
using System.Xml.Serialization;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.SkillEngine.Model;
using static Aion.GameServer.Network.Aion.ServerPackets.SmAttackStatus;
using static Aion.GameServer.SkillEngine.Model.Skill;
using SM_ATTACK_STATUS = Aion.GameServer.Network.Aion.ServerPackets.SmAttackStatus;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: skillengine/effect/MagicCounterAtkEffect (ViAl) : EffectTemplate. @XmlAttribute maxdmg; applyEffect→addToEffectedController; startEffect: anonymous ActionObserver(ENDSKILLCAST).endSkillCast→nested CounterObserver capturing outer+effect+effected: non-ITEM && MAGICAL→damage=min(maxdmg, maxHp.base/100f*Value), onAttack(MAGICCOUNTERATK, ..., Hoptype). Skill.SkillMethod/SkillType red-tolerated.</summary>
[XmlType("MagicCounterAtkEffect")]
public class MagicCounterAtkEffect : EffectTemplate
{
    [XmlAttribute]
    protected int maxdmg;

    // TODO bosses are resistent to this?
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
                int damage = Math.Min(outer.maxdmg, (int)(effected.GetGameStats().GetMaxHp().GetBase() / 100f * outer.Value));
                effected.GetController().OnAttack(effect, TYPE.MAGICCOUNTERATK, damage, true, LOG.MAGICCOUNTERATK, outer.Hoptype);
            }
        }
    }
}
