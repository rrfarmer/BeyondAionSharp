using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/worlds/inggison/SematariuxEggAI (@author Estrayl).</summary>
[AIName("sematariux_egg")]
public class SematariuxEggAI : NpcAI
{
    private ScheduledTask spawnTask;

    public SematariuxEggAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        spawnTask = ThreadPoolManager.GetInstance().Schedule(() => Spawn(281456, GetOwner().GetX(), GetOwner().GetY(), GetOwner().GetZ(), (sbyte)0),
            (long)System.TimeSpan.FromMinutes(4).TotalMilliseconds); // Java: 4, TimeUnit.MINUTES
    }

    protected override void HandleDied()
    {
        Npc sematariux = GetOwner().GetWorldMapInstance().GetNpc(216520);
        if (sematariux != null)
        {
            sematariux.GetEffectController().RemoveEffect(18726);
            sematariux.QueueSkill(19199, 1, 3000);
        }
        CancelSpawnTask();
        base.HandleDied();
    }

    protected override void HandleDespawned()
    {
        CancelSpawnTask();
        base.HandleDespawned();
    }

    private void CancelSpawnTask()
    {
        if (spawnTask != null && !spawnTask.IsCancelled)
        {
            spawnTask.Cancel(true);
        }
    }

    public override bool Ask(AIQuestion question)
    {
        return question switch
        {
            AIQuestion.ALLOW_DECAY or AIQuestion.REWARD_LOOT or AIQuestion.REWARD_AP_XP_DP_LOOT => false,
            _ => base.Ask(question),
        };
    }
}
