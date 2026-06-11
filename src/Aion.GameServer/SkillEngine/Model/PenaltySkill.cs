using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.SkillEngine.Model;

/// <summary>Java parity: skillengine/model/PenaltySkill : Skill. ctor super(skillTemplate, effector, skillLevel, effector, null); useSkill→useWithoutPropSkill + true; initializeSkillMethod→skillMethod=PENALTY. Skill base members red-tolerated.</summary>
public class PenaltySkill : Skill
{
    public PenaltySkill(SkillTemplate skillTemplate, Creature effector, int skillLevel)
        : base(skillTemplate, effector, skillLevel, effector, null)
    {
    }

    public override bool UseSkill()
    {
        base.UseWithoutPropSkill();
        return true;
    }

    public override void InitializeSkillMethod()
    {
        skillMethod = SkillMethod.PENALTY;
    }
}
