using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/tallocsHollow/KinquidAI (xTz, Sykra).</summary>
[AIName("kinquid")]
public class KinquidAI : AggressiveNpcAI
{
    private readonly AtomicBoolean isHome = new AtomicBoolean(true);
    private ScheduledTask skillTask;
    private ScheduledTask destroyerRespawnTask;

    public KinquidAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleCreatureAggro(Creature creature)
    {
        base.HandleCreatureAggro(creature);
        if (isHome.CompareAndSet(true, false))
        {
            GetPosition().GetWorldMapInstance().SetDoorState(48, false);
            CancelSkillTask();
            skillTask = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(ct => { RunSkillTask(); return ValueTask.CompletedTask; }, System.TimeSpan.FromMilliseconds(35000), System.TimeSpan.FromMilliseconds(35000));
            CancelDestroyerRespawnTask();
            destroyerRespawnTask = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(ct => { RespawnDestroyer(); return ValueTask.CompletedTask; }, System.TimeSpan.FromMilliseconds(0), System.TimeSpan.FromMilliseconds(25000));
        }
    }

    protected override void HandleBackHome()
    {
        CancelSkillTask();
        CancelDestroyerRespawnTask();
        isHome.Set(true);
        GetPosition().GetWorldMapInstance().SetDoorState(48, true);
        base.HandleBackHome();
        DespawnDestroyer();
    }

    protected override void HandleDespawned()
    {
        CancelSkillTask();
        CancelDestroyerRespawnTask();
        base.HandleDespawned();
    }

    protected override void HandleDied()
    {
        CancelSkillTask();
        CancelDestroyerRespawnTask();
        base.HandleDied();
    }

    private void CancelSkillTask()
    {
        if (skillTask != null && !skillTask.IsDone())
        {
            skillTask.Cancel(true);
            skillTask = null;
        }
    }

    private void CancelDestroyerRespawnTask()
    {
        if (destroyerRespawnTask != null && !destroyerRespawnTask.IsDone())
        {
            destroyerRespawnTask.Cancel(true);
            destroyerRespawnTask = null;
        }
    }

    private void RunSkillTask()
    {
        if (IsDead() || !GetPosition().IsSpawned())
        {
            CancelSkillTask();
            return;
        }
        SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 19233, 60, GetOwner()).UseNoAnimationSkill();
        ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            if (!IsDead() && GetPosition().IsSpawned())
                SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 19234, 60, GetOwner()).UseNoAnimationSkill();
            return ValueTask.CompletedTask;
        }, 3500L);
    }

    private void DespawnDestroyer()
    {
        Npc cleaveArmor = GetPosition().GetWorldMapInstance().GetNpc(282008);
        if (cleaveArmor != null)
            cleaveArmor.GetController().Delete();
        Npc accessoryDestruction = GetPosition().GetWorldMapInstance().GetNpc(282009);
        if (accessoryDestruction != null)
            accessoryDestruction.GetController().Delete();
    }

    private void RespawnDestroyer()
    {
        DespawnDestroyer();
        if (GetPosition().IsSpawned() && !IsDead() && !isHome.Get())
        {
            int spawnId = Rnd.NextBoolean() ? 282008 : 282009;
            switch (Rnd.Get(1, 3))
            {
                case 1:
                    Spawn(spawnId, 266.70685f, 680.6733f, 1167.2369f, (sbyte)0);
                    break;
                case 2:
                    Spawn(spawnId, 292.02466f, 719.7132f, 1169.3982f, (sbyte)0);
                    break;
                case 3:
                    Spawn(spawnId, 263.4334f, 716.73004f, 1170.3693f, (sbyte)0);
                    break;
            }
        }
    }
}
