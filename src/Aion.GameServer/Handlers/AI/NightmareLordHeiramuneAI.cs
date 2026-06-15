using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/nightmareCircus/NightmareLordHeiramuneAI (@author Ritsu).</summary>
[AIName("nightmarelordheiramune")]
public class NightmareLordHeiramuneAI : AggressiveNpcAI, HpPhases.PhaseHandler
{
    private readonly HpPhases hpPhases = new HpPhases(80, 50);
    private ScheduledTask? spawnTask;

    public NightmareLordHeiramuneAI(Npc owner) : base(owner)
    {
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        hpPhases.TryEnterNextPhase(this);
    }

    public void HandleHpPhase(int phaseHpPercent)
    {
        switch (phaseHpPercent)
        {
            case 80:
                StartSpawnTask();
                break;
            case 50:
                PacketSendUtility.BroadcastMessage(GetOwner(), 1501138);
                Spawn(233162, GetOwner().GetX() + 5, GetOwner().GetY() + 5, GetOwner().GetZ(), (sbyte)GetOwner().GetHeading());
                break;
        }
    }

    private void StartSpawnTask()
    {
        spawnTask = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ =>
        {
            if (!IsDead())
            {
                SpawnHelpers();
            }
            return ValueTask.CompletedTask;
        }, System.TimeSpan.FromMilliseconds(0), System.TimeSpan.FromMilliseconds(20000));
    }

    private void CancelTask()
    {
        if (spawnTask != null && !spawnTask.IsCancelled)
        {
            spawnTask.Cancel(true);
        }
    }

    protected override void HandleDied()
    {
        base.HandleDied();
        CancelTask();
        DespawnNpcs(233457, 233162);
    }

    protected override void HandleDespawned()
    {
        base.HandleDespawned();
        CancelTask();
    }

    protected override void HandleBackHome()
    {
        base.HandleBackHome();
        CancelTask();
        hpPhases.Reset();
    }

    private void DespawnNpcs(params int[] npcIds)
    {
        foreach (Npc npc in GetOwner().GetPosition().GetWorldMapInstance().GetNpcs(npcIds))
        {
            npc.GetController().Delete();
        }
    }

    private void SpawnHelpers()
    {
        PacketSendUtility.BroadcastMessage(GetOwner(), 1501139);
        Spawn(233457, 521.585f, 510.16528f, 199.59279f, (sbyte)30);
        Spawn(233457, 523.3747f, 621.1362f, 208.05113f, (sbyte)90);
    }
}
