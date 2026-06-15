using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/HomingNpcAI (ATracer).</summary>
[AIName("homing")]
public class HomingNpcAI : GeneralNpcAI
{
    public HomingNpcAI(Npc owner)
        : base(owner)
    {
    }

    public override void Think()
    {
        // homings are not thinking to return :)
    }

    public override AttackIntention ChooseAttackIntention()
    {
        if (GetTarget() != null && ChooseSkillAttack(false))
            return AttackIntention.SKILL_ATTACK;

        return AttackIntention.SIMPLE_ATTACK;
    }

    public override bool Ask(AIQuestion question)
    {
        return question switch
        {
            AIQuestion.ALLOW_DECAY or AIQuestion.ALLOW_RESPAWN or AIQuestion.REWARD_AP_XP_DP_LOOT => false,
            _ => base.Ask(question),
        };
    }
}
