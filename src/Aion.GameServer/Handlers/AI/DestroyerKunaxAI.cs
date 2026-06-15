using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/idgelDome/DestroyerKunaxAI (Estrayl).</summary>
[AIName("destroyer_kunax")]
public class DestroyerKunaxAI : AggressiveNpcAI
{
    public DestroyerKunaxAI(Npc owner)
        : base(owner)
    {
    }

    public override AttackIntention ChooseAttackIntention()
    {
        double dist = 0;
        if (GetTarget() != null)
        {
            dist = PositionUtil.GetDistance(GetOwner(), GetTarget()) - GetObjectTemplate().GetBoundRadius().GetMaxOfFrontAndSide()
                - GetTarget().GetObjectTemplate().GetBoundRadius().GetMaxOfFrontAndSide();
        }
        if (dist > 3 && dist <= 30)
        {
            Aion.GameServer.SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 21550, 56, GetTarget()).UseSkill();
            return AttackIntention.SKILL_ATTACK;
        }
        return base.ChooseAttackIntention();
    }
}
