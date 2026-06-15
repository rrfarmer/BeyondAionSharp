using System;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/tiamatStrongHold/DistortedSpaceAI (@author Cheatkiller).</summary>
[AIName("distortedspace")]
public class DistortedSpaceAI : NpcAI
{
    private ScheduledTask task;

    public DistortedSpaceAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        UseSkill();
    }

    private void UseSkill()
    {
        task = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ =>
        {
            if (GetOwner().GetNpcId() == 283097)
                AIActions.UseSkill(this, 20740);
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(2000));

        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            CancelTask();
            if (GetOwner().GetNpcId() == 283097)
                AIActions.UseSkill(this, 20742);
            GetOwner().GetController().Die();
            return ValueTask.CompletedTask;
        }, 8000L);
    }

    private void CancelTask()
    {
        if (task != null && !task.IsCancelled)
        {
            task.Cancel(true);
        }
    }

    protected override void HandleDied()
    {
        base.HandleDied();
        CancelTask();
        AIActions.DeleteOwner(this);
    }

    protected override void HandleDespawned()
    {
        base.HandleDespawned();
        CancelTask();
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
