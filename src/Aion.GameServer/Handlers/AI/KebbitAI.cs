using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/stonespearReach/KebbitAI (@author Yeats).</summary>
[AIName("stonespear_kebbit")]
public class KebbitAI : GeneralNpcAI
{
    private ScheduledTask despawnTask;

    public KebbitAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        despawnTask = ThreadPoolManager.GetInstance().Schedule(_ => { GetOwner().GetController().Delete(); return ValueTask.CompletedTask; }, 15500L); // 15,5s
    }

    protected override void HandleDied()
    {
        CancelTask();
        base.HandleDied();
        GetOwner().GetController().Delete();
    }

    private void CancelTask()
    {
        if (despawnTask != null && !despawnTask.IsDone())
            despawnTask.Cancel(false);
    }

    public override bool Ask(AIQuestion question)
    {
        switch (question)
        {
            case AIQuestion.ALLOW_RESPAWN:
            case AIQuestion.REWARD_AP_XP_DP_LOOT:
            case AIQuestion.REWARD_LOOT:
            case AIQuestion.ALLOW_DECAY:
                return false;
            default:
                return base.Ask(question);
        }
    }

    protected override void HandleAttack(Creature creature)
    {
        // do nothing
    }
}
