using System;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/tiamatStrongHold/FireStormAI (@author Cheatkiller).</summary>
[AIName("tahabatafirestorm")]
public class FireStormAI : NpcAI
{
    private ScheduledTask task;

    public FireStormAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        int skill = GetNpcId() == 283102 ? 20753 : 20759; // 4.0
        task = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ => { AIActions.UseSkill(this, skill); return ValueTask.CompletedTask; }, TimeSpan.FromMilliseconds(0), TimeSpan.FromMilliseconds(1000));

        if (GetNpcId() != 283102) // 4.0
            Despawn();
    }

    private void Despawn()
    {
        ThreadPoolManager.GetInstance().Schedule(_ => { GetOwner().GetController().Delete(); return ValueTask.CompletedTask; }, 20000L);
    }

    protected override void HandleDespawned()
    {
        task.Cancel(true);
        base.HandleDespawned();
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
