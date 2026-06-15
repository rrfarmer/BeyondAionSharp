using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/tiamatStrongHold/BladeStormAI (@author Cheatkiller).</summary>
[AIName("bladestorm")]
public class BladeStormAI : NpcAI
{
    private ScheduledTask? spinTask;

    public BladeStormAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        spinTask = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ =>
        {
            AIActions.UseSkill(this, 20748);
            return System.Threading.Tasks.ValueTask.CompletedTask;
        }, System.TimeSpan.FromMilliseconds(0), System.TimeSpan.FromMilliseconds(1000));
        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            GetOwner().GetController().Delete();
            return System.Threading.Tasks.ValueTask.CompletedTask;
        }, 10000L);
    }

    protected override void HandleDespawned()
    {
        spinTask!.Cancel(true);
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
