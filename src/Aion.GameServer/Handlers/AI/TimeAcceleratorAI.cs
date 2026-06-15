using System;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/tiamatStrongHold/TimeAcceleratorAI (@author Cheatkiller).</summary>
[AIName("timeaccelerator")]
public class TimeAcceleratorAI : NpcAI
{
    public TimeAcceleratorAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleCreatureSee(Creature creature)
    {
        CheckDistance(creature);
    }

    protected override void HandleCreatureMoved(Creature creature)
    {
        CheckDistance(creature);
    }

    private void CheckDistance(Creature creature)
    {
        if (PositionUtil.IsInRange(GetOwner(), creature, 5) && !creature.GetEffectController().HasAbnormalEffect(20727))
            AIActions.UseSkill(this, 20727);
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        Despawn();
    }

    private void Despawn()
    {
        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            GetOwner().GetController().Delete();
            return ValueTask.CompletedTask;
        }, 20000L);
    }

    public override bool Ask(AIQuestion question)
    {
        switch (question)
        {
            case AIQuestion.ALLOW_DECAY:
            case AIQuestion.ALLOW_RESPAWN:
            case AIQuestion.REWARD_AP_XP_DP_LOOT:
                return false;
            default:
                return base.Ask(question);
        }
    }
}
