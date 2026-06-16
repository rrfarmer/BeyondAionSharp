using System;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: skillengine/effect/OneTimeBoostSkillAttackEffect (ATracer) : BufEffect. @XmlAttribute count/type(SkillType); switch-arrow PHYSICAL/MAGICAL/ALL→switch statement; stateful anonymous AttackCalcObserver(boostCount)→nested BoostObserver capturing outer+effect+percent; getBasePhysicalDamageMultiplier(isSkill)/getBaseMagicalDamageMultiplier overrides; removeEffect→schedule(100ms) removeEffect(skillId). AttackCalcObserver/SkillType red-tolerated.</summary>
[XmlType("OneTimeBoostSkillAttackEffect")]
public class OneTimeBoostSkillAttackEffect : BufEffect
{
    [XmlAttribute]
    public int count;

    [XmlAttribute]
    public SkillType type;

    public override void StartEffect(Effect effect)
    {
        base.StartEffect(effect);

        float percent = 1.0f + Value / 100.0f;
        switch (type)
        {
            case SkillType.PHYSICAL:
            case SkillType.MAGICAL:
            case SkillType.ALL:
                effect.AddObserver(effect.GetEffected(), new BoostObserver(this, effect, percent));
                break;
        }
    }

    private void RemoveEffect(Effect effect)
    {
        ThreadPoolManager.GetInstance().Schedule(ct => { effect.GetEffected().GetEffectController().RemoveEffect(effect.GetSkillId()); return ValueTask.CompletedTask; }, TimeSpan.FromMilliseconds(100));
    }

    private sealed class BoostObserver : AttackCalcObserver
    {
        private readonly OneTimeBoostSkillAttackEffect outer;
        private readonly Effect effect;
        private readonly float percent;
        private int boostCount = 0;

        public BoostObserver(OneTimeBoostSkillAttackEffect outer, Effect effect, float percent)
        {
            this.outer = outer;
            this.effect = effect;
            this.percent = percent;
        }

        public override float GetBasePhysicalDamageMultiplier(bool isSkill)
        {
            if (isSkill && outer.type != SkillType.MAGICAL && boostCount++ < outer.count)
            {
                if (boostCount == outer.count)
                    outer.RemoveEffect(effect);
                return percent;
            }
            return 1.0f;
        }

        public override float GetBaseMagicalDamageMultiplier()
        {
            if (outer.type != SkillType.PHYSICAL && boostCount++ < outer.count)
            {
                if (boostCount == outer.count)
                    outer.RemoveEffect(effect);
                return percent;
            }
            return 1.0f;
        }
    }
}
