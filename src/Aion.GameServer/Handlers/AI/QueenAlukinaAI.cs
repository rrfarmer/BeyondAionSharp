using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/empyreanCrucible/QueenAlukinaAI (@author Luzien).</summary>
[AIName("alukina_emp")]
public class QueenAlukinaAI : AggressiveNpcAI, HpPhases.PhaseHandler
{
    private readonly HpPhases hpPhases = new HpPhases(75, 50, 25);
    private ScheduledTask? task;

    public QueenAlukinaAI(Npc owner) : base(owner)
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
        SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 17899, 41, GetTarget()).UseNoAnimationSkill();

        switch (phaseHpPercent)
        {
            case 75:
                ScheduleSkill(17900, 4500);
                PacketSendUtility.BroadcastMessage(GetOwner(), 340487, 10000);
                ScheduleSkill(17899, 14000);
                ScheduleSkill(17900, 18000);
                break;
            case 50:
                ScheduleSkill(17280, 4500);
                ScheduleSkill(17902, 8000);
                break;
            case 25:
                task = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ =>
                {
                    if (IsDead())
                    {
                        CancelTask();
                    }
                    else
                    {
                        SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 17901, 41, GetTarget()).UseNoAnimationSkill();
                        ScheduleSkill(17902, 5500);
                        ScheduleSkill(17902, 7500);
                    }
                    return ValueTask.CompletedTask;
                }, System.TimeSpan.FromMilliseconds(4500), System.TimeSpan.FromMilliseconds(20000));
                break;
        }
    }

    private void CancelTask()
    {
        if (task != null && !task.IsCancelled)
            task.Cancel(true);
    }

    private void ScheduleSkill(int skill, int delay)
    {
        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            if (!IsDead())
            {
                SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), skill, 41, GetTarget()).UseNoAnimationSkill();
            }
            return ValueTask.CompletedTask;
        }, delay);
    }
}
