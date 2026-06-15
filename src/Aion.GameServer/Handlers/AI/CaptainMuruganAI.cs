using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/esoterrace/CaptainMuruganAI (@author xTz).</summary>
[AIName("captain_murugan")]
public class CaptainMuruganAI : AggressiveNpcAI
{
    private readonly AtomicBoolean isAggred = new AtomicBoolean(false);
    private ScheduledTask task;
    private ScheduledTask specialSkillTask;

    public CaptainMuruganAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        if (isAggred.CompareAndSet(false, true))
        {
            StartTaskEvent();
        }
    }

    private void StartTaskEvent()
    {
        VisibleObject target = GetTarget();
        if (target is Player)
        {
            SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 19324, 10, target).UseNoAnimationSkill();
        }
        task = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ =>
        {
            if (IsDead())
            {
                CancelTask();
            }
            else
            {
                PacketSendUtility.BroadcastMessage(GetOwner(), 1500194);
                SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 19325, 5, GetOwner()).UseNoAnimationSkill();
                if (GetLifeStats().GetHpPercentage() < 50)
                {
                    specialSkillTask = ThreadPoolManager.GetInstance().Schedule(_ =>
                    {
                        if (!IsDead())
                        {
                            PacketSendUtility.BroadcastMessage(GetOwner(), 1500193);
                            VisibleObject target = GetTarget();
                            if (target is Player)
                            {
                                SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 19324, 10, target).UseNoAnimationSkill();
                            }
                            specialSkillTask = ThreadPoolManager.GetInstance().Schedule(_ =>
                            {
                                if (!IsDead())
                                {
                                    VisibleObject target = GetTarget();
                                    if (target is Player)
                                    {
                                        SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 19324, 10, target).UseNoAnimationSkill();
                                    }
                                }
                                return ValueTask.CompletedTask;
                            }, 4000L);
                        }
                        return ValueTask.CompletedTask;
                    }, 10000L);
                }
            }
            return ValueTask.CompletedTask;
        }, System.TimeSpan.FromMilliseconds(20000), System.TimeSpan.FromMilliseconds(20000));
    }

    private void CancelTask()
    {
        if (task != null && !task.IsDone())
        {
            task.Cancel(true);
        }
    }

    private void CancelSpecialSkillTask()
    {
        if (specialSkillTask != null && !specialSkillTask.IsDone())
        {
            specialSkillTask.Cancel(true);
        }
    }

    protected override void HandleBackHome()
    {
        CancelTask();
        CancelSpecialSkillTask();
        base.HandleBackHome();
        isAggred.Set(false);
    }

    protected override void HandleDespawned()
    {
        CancelTask();
        CancelSpecialSkillTask();
        base.HandleDespawned();
        isAggred.Set(false);
    }

    protected override void HandleDied()
    {
        CancelTask();
        CancelSpecialSkillTask();
        PacketSendUtility.BroadcastMessage(GetOwner(), 1500195);
        base.HandleDied();
        isAggred.Set(false);
    }
}
