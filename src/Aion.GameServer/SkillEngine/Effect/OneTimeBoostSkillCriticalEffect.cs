using System.Xml.Serialization;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: skillengine/effect/OneTimeBoostSkillCriticalEffect (Sippolo) : EffectTemplate. @XmlAttribute count/percent; applyEffect→addToEffectedController; startEffect: anonymous AttackerCriticalStatusObserver(CRITICAL, count, value, percent) overriding checkAttackerCriticalStatus→nested CritObserver capturing effect: stat==Status&&isSkill→GetCount&lt;=1 endEffect else DecreaseCount, AcStatus.SetResult(true) else (false). AttackerCriticalStatus red-tolerated.</summary>
[XmlType("OneTimeBoostSkillCriticalEffect")]
public class OneTimeBoostSkillCriticalEffect : EffectTemplate
{
    [XmlAttribute]
    public int count;
    [XmlAttribute]
    public bool percent;

    public override void ApplyEffect(Effect effect)
    {
        effect.AddToEffectedController();
    }

    public override void StartEffect(Effect effect)
    {
        effect.AddObserver(effect.GetEffected(), new CritObserver(effect, AttackStatus.CRITICAL, count, Value, percent));
    }

    private sealed class CritObserver : AttackerCriticalStatusObserver
    {
        private readonly Effect effect;

        public CritObserver(Effect effect, AttackStatus status, int count, int value, bool percent)
            : base(status, count, value, percent)
        {
            this.effect = effect;
        }

        public override AttackerCriticalStatus CheckAttackerCriticalStatus(AttackStatus stat, bool isSkill)
        {
            if (stat == Status && isSkill)
            {
                if (GetCount() <= 1)
                    effect.EndEffect();
                else
                    DecreaseCount();

                AcStatus.SetResult(true);
            }
            else
                AcStatus.SetResult(false);

            return AcStatus;
        }
    }
}
