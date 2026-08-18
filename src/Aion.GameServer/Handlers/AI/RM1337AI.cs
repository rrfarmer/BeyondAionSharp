using System;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// @author Luzien
/// </summary>
[AIName("rm_1337")]
public class RM1337AI : AggressiveNpcAI
{
    private readonly AtomicBoolean isHome = new AtomicBoolean(true);
    private readonly AtomicBoolean isEventStarted = new AtomicBoolean(false);
    private ScheduledTask task1, task2;

    public RM1337AI(Npc owner) : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        PacketSendUtility.BroadcastMessage(GetOwner(), 1500229, 2000);
    }

    protected override void HandleDespawned()
    {
        CancelTask();
        base.HandleDespawned();
    }

    protected override void HandleDied()
    {
        CancelTask();
        PacketSendUtility.BroadcastMessage(GetOwner(), 1500231);
        base.HandleDied();
    }

    protected override void HandleBackHome()
    {
        CancelTask();
        base.HandleBackHome();
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        if (isHome.CompareAndSet(true, false))
        {
            StartSkillTask1();
        }
        if (GetLifeStats().GetHpPercentage() <= 75)
        {
            if (isEventStarted.CompareAndSet(false, true))
            {
                StartSkillTask2();
            }
        }
    }

    private void CancelTask()
    {
        if (task1 != null && !task1.IsCancelled)
        {
            task1.Cancel(true);
        }
        if (task2 != null && !task2.IsCancelled)
        {
            task2.Cancel(true);
        }
    }

    private void StartSkillTask1()
    {
        task1 = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(ct =>
        {
            if (IsDead())
            {
                CancelTask();
            }
            else
            {
                if (GetOwner().GetCastingSkill() != null)
                    return ValueTask.CompletedTask;
                if (GetLifeStats().GetHpPercentage() <= 50)
                {
                    if (Rnd.NextBoolean())
                    {
                        SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 19550, 10, GetRandomTarget()).UseNoAnimationSkill();
                    }
                    else
                    {
                        Creature target = GetRandomTarget();
                        SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 19552, 10, target).UseNoAnimationSkill();
                        ThreadPoolManager.GetInstance().Schedule(ct2 =>
                        {
                            if (!IsDead())
                            {
                                SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 19553, 10, target).UseNoAnimationSkill();
                            }
                            return ValueTask.CompletedTask;
                        }, 4000L);
                    }
                }
                else
                    SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 19550, 10, GetRandomTarget()).UseNoAnimationSkill();
            }
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(10000), TimeSpan.FromMilliseconds(23000));
    }

    private void StartSkillTask2()
    {
        task2 = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(ct =>
        {
            if (IsDead())
            {
                CancelTask();
            }
            else
            {
                GetOwner().GetController().CancelCurrentSkill(null);
                PacketSendUtility.BroadcastMessage(GetOwner(), 1500230);
                SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 19551, 10, GetTarget()).UseNoAnimationSkill();
                SpawnSparks();
            }
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(0), TimeSpan.FromMilliseconds(60000));
    }

    private void SpawnSparks()
    {
        ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            if (!IsDead())
            {
                int count = Rnd.Get(8, 12);
                for (int i = 0; i < count; i++)
                {
                    RndSpawnInRange(282373, 3, 12); // SparkOfDarknessAI carries retail's five seconds
                }
            }
            return ValueTask.CompletedTask;
        }, 4000L);
    }

    private Creature GetRandomTarget()
    {
        return GetAggroList().GetTarget(AggroTarget.RANDOM, 37);
    }
}
