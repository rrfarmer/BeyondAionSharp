using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/tiamatStrongHold/SlowTimeAI (Cheatkiller).</summary>
[AIName("slowedtime")]
public class SlowTimeAI : NpcAI
{
    public SlowTimeAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleCreatureSee(Creature creature)
    {
        CheckDistance(this, creature);
    }

    protected override void HandleCreatureMoved(Creature creature)
    {
        CheckDistance(this, creature);
    }

    private void CheckDistance(NpcAI ai, Creature creature)
    {
        if (creature is Player)
        {
            if (PositionUtil.IsInRange(GetOwner(), creature, 50) && !creature.GetEffectController().HasAbnormalEffect(20728))
            {
                AIActions.UseSkill(this, 20728);
            }
        }
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
