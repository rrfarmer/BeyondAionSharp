using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Instance;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/empyreanCrucible/SpectralWarriorAI (@author Luzien, w4terbomb).</summary>
[AIName("spectral_warrior")]
public class SpectralWarriorAI : AggressiveNpcAI, HpPhases.PhaseHandler
{
    private readonly HpPhases hpPhases = new HpPhases(50);
    private ScheduledTask? attackTask;

    public SpectralWarriorAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleDespawned()
    {
        CancelTask();
        base.HandleDespawned();
    }

    protected override void HandleDied()
    {
        CancelTask();
        base.HandleDied();
    }

    protected override void HandleBackHome()
    {
        CancelTask();
        base.HandleBackHome();
        hpPhases.Reset();
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        hpPhases.TryEnterNextPhase(this);
    }

    public void HandleHpPhase(int phaseHpPercent)
    {
        GetPosition().GetWorldMapInstance().GetInstanceHandler().OnChangeStage(StageType.START_STAGE_6_ROUND_5);
        GetOwner().QueueSkill(19546, 10);
        List<Npc> raised = ResurrectAllies();
        attackTask = ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            if (IsDead() || !(GetTarget() is Creature target))
                return ValueTask.CompletedTask;
            foreach (Npc npc in raised.Where(npc => !npc.IsDead()))
                npc.GetAggroList().AddHate(target, 1000);
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(10000));
    }

    private List<Npc> ResurrectAllies()
    {
        List<Npc> raised = new List<Npc>();
        GetKnownList().ForEachNpc(npc =>
        {
            if (npc.IsDead())
                return;
            int raisedNpcId = npc.GetNpcId() switch
            {
                205413 => 217576,
                205414 => 217577,
                _ => 0,
            };
            if (raisedNpcId == 0)
                return;
            VisibleObject spawned = Spawn(raisedNpcId, npc.GetX(), npc.GetY(), npc.GetZ(), (sbyte)npc.GetHeading());
            npc.GetController().Delete();
            if (spawned is Npc raisedNpc)
                raised.Add(raisedNpc);
        });
        return raised;
    }

    private void CancelTask()
    {
        if (attackTask != null && !attackTask.IsDone())
            attackTask.Cancel(true);
    }
}
