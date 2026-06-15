using System;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// @author Luzien
/// </summary>
[AIName("warrior_preceptor")]
public class WarriorPreceptorAI : AggressiveNpcAI
{
    private AtomicBoolean isHome = new AtomicBoolean(true);
    private ScheduledTask task;

    public WarriorPreceptorAI(Npc owner) : base(owner)
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
        PacketSendUtility.BroadcastMessage(GetOwner(), 1500208);
        base.HandleDied();
    }

    protected override void HandleBackHome()
    {
        CancelTask();
        isHome.Set(true);
        base.HandleBackHome();
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        if (isHome.CompareAndSet(true, false))
        {
            StartSkillTask();
        }
    }

    private void StartSkillTask()
    {
        task = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ =>
        {
            if (IsDead())
            {
                CancelTask();
            }
            else
            {
                StartSkillEvent();
            }
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(30000), TimeSpan.FromMilliseconds(30000));
    }

    private void CancelTask()
    {
        if (task != null && !task.IsCancelled)
        {
            task.Cancel(true);
        }
    }

    private void StartSkillEvent()
    {
        PacketSendUtility.BroadcastMessage(GetOwner(), 1500207);
        SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 19595, 10, GetRandomTarget()).UseNoAnimationSkill(); // Divine Grasp II
        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            if (!IsDead())
            {
                SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 19596, 15, GetOwner()).UseNoAnimationSkill(); // Wrathful Wave II
            }
            return ValueTask.CompletedTask;
        }, 6000L);
    }

    private Creature GetRandomTarget()
    {
        return GetAggroList().GetTarget(AggroTarget.RANDOM, 15);
    }
}
