using System.Xml.Serialization;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: skillengine/effect/BoostSkillCostEffect (Rama, Sippolo) : BufEffect. @XmlAttribute percent; super.startEffect then anonymous ActionObserver(BOOSTSKILLCOST).boostSkillCost(skill)→skill.setBoostSkillCost(value) → nested BoostSkillCostObserver capturing outer. Skill red-tolerated.</summary>
[XmlType("BoostSkillCostEffect")]
public class BoostSkillCostEffect : BufEffect
{
    [XmlAttribute]
    protected bool percent;

    public override void StartEffect(Effect effect)
    {
        base.StartEffect(effect);

        effect.AddObserver(effect.GetEffected(), new BoostSkillCostObserver(this));
    }

    private sealed class BoostSkillCostObserver : ActionObserver
    {
        private readonly BoostSkillCostEffect outer;

        public BoostSkillCostObserver(BoostSkillCostEffect outer)
            : base(ObserverType.BOOSTSKILLCOST)
        {
            this.outer = outer;
        }

        public override void BoostSkillCost(Skill skill)
        {
            skill.SetBoostSkillCost(outer.Value);
        }
    }
}
