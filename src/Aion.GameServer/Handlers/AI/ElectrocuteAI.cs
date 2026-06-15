using System.Threading;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/tiamatStrongHold/ElectrocuteAI (@author Luzien).</summary>
[AIName("electrocute")]
public class ElectrocuteAI : NpcAI
{
    private ScheduledTask task;

    public ElectrocuteAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        task = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ =>
        {
            AIActions.UseSkill(this, 20757);
            return ValueTask.CompletedTask;
        }, System.TimeSpan.Zero, System.TimeSpan.FromMilliseconds(2000));
        Despawn();
    }

    private void Despawn()
    {
        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            GetOwner().GetController().Delete();
            return ValueTask.CompletedTask;
        }, 10500L);
    }

    protected override void HandleDespawned()
    {
        task.Cancel(true);
        base.HandleDespawned();
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
