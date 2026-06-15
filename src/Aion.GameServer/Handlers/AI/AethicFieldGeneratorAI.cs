using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/beshmundirTemple/AethicFieldGeneratorAI (Tibald).</summary>
[AIName("aethic_field_generator")]
public class AethicFieldGeneratorAI : GeneralNpcAI, HpPhases.PhaseHandler
{
    private readonly HpPhases hpPhases = new HpPhases(75, 50, 25);
    private AtomicBoolean isAggred = new AtomicBoolean(false);
    private ScheduledTask aggroTask;

    public AethicFieldGeneratorAI(Npc owner)
        : base(owner)
    {
    }

    public override bool CanThink()
    {
        return false;
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 19127, 1, GetOwner()).UseNoAnimationSkill();
    }

    protected override void HandleAttack(Creature creature)
    {
        if (isAggred.CompareAndSet(false, true))
        {
            GetPosition().GetWorldMapInstance().SetDoorState(471, false);
            AggroTask();
        }
        base.HandleAttack(creature);
        hpPhases.TryEnterNextPhase(this);
    }

    public void HandleHpPhase(int phaseHpPercent)
    {
        switch (phaseHpPercent)
        {
            case 75:
                Spawn(216532, 1364.3739f, 88.38297f, 248.39449f, (sbyte)37);
                Spawn(216532, 1349.2109f, 87.81685f, 248.37459f, (sbyte)5);
                break;
            case 50:
                Spawn(216532, 1362.2529f, 88.02893f, 248.40222f, (sbyte)53);
                Spawn(216532, 1352.1556f, 87.69477f, 248.38733f, (sbyte)4);
                Spawn(216532, 1356.2352f, 90.32507f, 247.75319f, (sbyte)92);
                break;
            case 25:
                Spawn(216532, 1366.1492f, 84.480515f, 248.59457f, (sbyte)51);
                Spawn(216532, 1349.3264f, 83.13163f, 248.59457f, (sbyte)6);
                Spawn(216532, 1354.3655f, 85.42517f, 248.59457f, (sbyte)27);
                Spawn(216532, 1360.4175f, 86.17866f, 248.59457f, (sbyte)34);
                break;
        }
    }

    private void AggroTask()
    {
        aggroTask = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ =>
        {
            if (IsDead())
            {
                CancelAggroTask();
            }
            else
            {
                if (!IsInRangePlayer())
                {
                    HandleBackHome();
                    GetOwner().GetController().LoseAggro(true);
                }
            }
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(2000), TimeSpan.FromMilliseconds(2000));
    }

    private bool IsInRangePlayer()
    {
        return GetKnownList().StreamVisiblePlayers().Any(player => IsInRange(player, 40) && !player.IsDead());
    }

    private void CancelAggroTask()
    {
        if (aggroTask != null && !aggroTask.IsDone())
        {
            aggroTask.Cancel(true);
        }
    }

    protected override void HandleBackHome()
    {
        HandleFinishAttack();
        CancelAggroTask();
        WorldMapInstance instance = GetPosition().GetWorldMapInstance();
        if (instance != null)
        {
            instance.SetDoorState(471, true);
            DeleteNpcs(instance.GetNpcs(216532));
        }
        isAggred.Set(false);
        base.HandleBackHome();
        hpPhases.Reset();
    }

    protected override void HandleDied()
    {
        WorldMapInstance instance = GetPosition().GetWorldMapInstance();
        if (instance != null)
        {
            instance.SetDoorState(471, true);
            DeleteNpcs(instance.GetNpcs(216532));
        }
        CancelAggroTask();
        base.HandleDied();
    }

    private void DeleteNpcs(List<Npc> npcs)
    {
        foreach (Npc npc in npcs)
        {
            if (npc != null)
            {
                npc.GetController().Delete();
            }
        }
    }
}
