using System;
using System.Linq;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/siege/SpringAI (@author xTz, Sykra).</summary>
[AIName("spring")]
public class SpringAI : NpcAI
{
    private ScheduledTask healCheckTask;

    public SpringAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        healCheckTask = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ => { CheckForHeal(); return ValueTask.CompletedTask; }, TimeSpan.FromMilliseconds(5000), TimeSpan.FromMilliseconds(5000));
    }

    protected override void HandleDied()
    {
        base.HandleDied();
        CancelTasks();
    }

    protected override void HandleDespawned()
    {
        base.HandleDespawned();
        CancelTasks();
    }

    public override bool Ask(AIQuestion question)
    {
        switch (question)
        {
            case AIQuestion.ALLOW_DECAY:
            case AIQuestion.REWARD_AP_XP_DP_LOOT:
            case AIQuestion.REWARD_LOOT:
                return false;
            default:
                return base.Ask(question);
        }
    }

    private void CancelTasks()
    {
        if (healCheckTask != null && !healCheckTask.IsDone())
        {
            healCheckTask.Cancel(true);
            healCheckTask = null;
        }
    }

    private void CheckForHeal()
    {
        if (IsDead() || !GetPosition().IsSpawned())
            return;
        bool targetNearby = GetKnownList().Stream()
            .Any(o => o.Get() is Creature creature && GetRace() == creature.GetRace() && !creature.IsDead() && !creature.GetLifeStats().IsFullyRestoredHp()
            && IsInRange(creature, 10) && !creature.GetEffectController().HasAbnormalEffect(19116));
        if (targetNearby)
            DoHeal();
    }

    private void DoHeal()
    {
        AIActions.TargetSelf(this);
        AIActions.UseSkill(this, 19116);
    }
}
