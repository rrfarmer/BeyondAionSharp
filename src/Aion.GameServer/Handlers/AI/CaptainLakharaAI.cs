using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Ai;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/theHexway/CaptainLakharaAI (@author Sykra).</summary>
[AIName("captain_lakhara")]
public class CaptainLakharaAI : SummonerAI
{
    private ScheduledTask? despawnTask;

    public CaptainLakharaAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleIndividualSpawnedSummons(Percentage percent)
    {
        GetOwner().ClearQueuedSkills();
        GetOwner().QueueSkill(17497, 65, 0);
        CancelDespawnTask();
        foreach (SummonGroup group in percent.GetSummons())
            SummonNpcWithSmoke(group);
        despawnTask = ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            foreach (SummonGroup group in percent.GetSummons())
                DespawnNpc(group.GetNpcId());
            return System.Threading.Tasks.ValueTask.CompletedTask;
        }, 25000L);
    }

    protected override void HandleDespawned()
    {
        base.HandleDespawned();
        CancelDespawnTask();
    }

    protected override void HandleBackHome()
    {
        base.HandleBackHome();
        CancelDespawnTask();
    }

    protected override void HandleDied()
    {
        base.HandleDied();
        CancelDespawnTask();
    }

    private void CancelDespawnTask()
    {
        if (despawnTask != null && !despawnTask.IsDone())
            despawnTask.Cancel(true);
    }

    private void DespawnNpc(int npcId)
    {
        foreach (Npc npc in GetPosition().GetWorldMapInstance().GetNpcs(npcId))
            npc.GetController().DeleteIfAliveOrCancelRespawn();
    }

    private void SummonNpcWithSmoke(SummonGroup summon)
    {
        SpawnHelpers(summon);
        Npc smoke = (Npc)Spawn(282465, summon.GetX(), summon.GetY(), summon.GetZ(), (sbyte)summon.GetH());
        smoke.GetController().Delete();
    }
}
