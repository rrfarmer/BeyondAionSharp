using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/tallocsHollow/MosquaEggAI (@author xTz, Sykra).</summary>
[AIName("mosquaegg")]
public class MosquaEggAI : AggressiveNpcAI
{
    private ScheduledTask supraklawSpawnTask;

    public MosquaEggAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        supraklawSpawnTask = ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            Spawn(217132, GetPosition().GetX(), GetPosition().GetY(), GetPosition().GetZ(), (sbyte)GetPosition().GetHeading());
            GetOwner().GetController().Delete();
            return ValueTask.CompletedTask;
        }, 17000L);
    }

    protected override void HandleDespawned()
    {
        base.HandleDespawned();
        CancelSpawnTask();
    }

    protected override void HandleDied()
    {
        base.HandleDied();
        CancelSpawnTask();
    }

    public override bool Ask(AIQuestion question)
    {
        switch (question)
        {
            case AIQuestion.ALLOW_DECAY:
            case AIQuestion.REWARD_AP_XP_DP_LOOT:
            case AIQuestion.REWARD_LOOT:
                return false;
            default:
                return base.Ask(question);
        }
    }

    private void CancelSpawnTask()
    {
        if (supraklawSpawnTask != null && !supraklawSpawnTask.IsDone())
        {
            supraklawSpawnTask.Cancel(true);
            supraklawSpawnTask = null;
        }
    }
}
