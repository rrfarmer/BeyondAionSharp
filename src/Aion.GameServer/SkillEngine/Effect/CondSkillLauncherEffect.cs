using System.Xml.Serialization;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.SkillEngine;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: skillengine/effect/CondSkillLauncherEffect (Sippolo) : EffectTemplate. @XmlAttribute(name="skill_id")/type(HealType); applyEffect→addToEffectedController; startEffect: anonymous ActionObserver(HP_CHANGED) w/ conditionalEffect field + synchronized + onRemoved→nested CondObserver capturing outer+effect: hp&lt;=Value*maxHp/100 && null→applyEffectDirectly (PASSIVE→duration 0 else null int?), else above-threshold→endEffect+null; lock(this). ActivationAttribute/SkillEngine red-tolerated.</summary>
[XmlType("CondSkillLauncherEffect")]
public class CondSkillLauncherEffect : EffectTemplate
{
    [XmlAttribute("skill_id")]
    protected int skillId;
    [XmlAttribute]
    protected HealType type;

    // TODO what if you fall? effect is not applied? what if you use skill that consume hp?
    public override void ApplyEffect(Effect effect)
    {
        effect.AddToEffectedController();
    }

    public override void StartEffect(Effect effect)
    {
        effect.AddObserver(effect.GetEffected(), new CondObserver(this, effect));
    }

    private sealed class CondObserver : ActionObserver
    {
        private readonly CondSkillLauncherEffect outer;
        private readonly Effect effect;
        private Effect conditionalEffect;

        public CondObserver(CondSkillLauncherEffect outer, Effect effect)
            : base(ObserverType.HP_CHANGED)
        {
            this.outer = outer;
            this.effect = effect;
        }

        public override void HpChanged(int hpValue)
        {
            bool hpAtOrBelowThreshold = hpValue <= outer.Value * effect.GetEffected().GetLifeStats().GetMaxHp() / 100;
            lock (this)
            {
                if (hpAtOrBelowThreshold && conditionalEffect == null)
                {
                    bool permanent = effect.GetSkillTemplate().GetActivationAttribute() == ActivationAttribute.PASSIVE;
                    int? duration = permanent ? 0 : (int?)null; // passive skills like Determination have no time limit
                    conditionalEffect = SkillEngine.GetInstance().ApplyEffectDirectly(outer.skillId, effect.GetEffected(), effect.GetEffected(), duration, null);
                }
                else if (!hpAtOrBelowThreshold && conditionalEffect != null)
                {
                    conditionalEffect.EndEffect();
                    conditionalEffect = null;
                }
            }
        }

        public override void OnRemoved()
        {
            if (conditionalEffect != null)
                conditionalEffect.EndEffect();
        }
    }
}
