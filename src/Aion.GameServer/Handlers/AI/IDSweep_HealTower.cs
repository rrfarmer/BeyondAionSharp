using System;
using System.Linq;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/theShugoEmperorsVault/IDSweep_HealTower (@author Yeats).</summary>
[AIName("idsweep_healtower")]
public class IDSweep_HealTower : GeneralNpcAI
{
    private ScheduledTask schedule;

    public IDSweep_HealTower(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        schedule = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ =>
        {
            TryHeal();
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(2000), TimeSpan.FromMilliseconds(3000));
    }

    protected override void HandleDespawned()
    {
        base.HandleDespawned();
        CancelTask();
    }

    protected override void HandleDied()
    {
        base.HandleDied();
        CancelTask();
    }

    private void CancelTask()
    {
        if (schedule != null && !schedule.IsCancelled)
        {
            schedule.Cancel(true);
        }
    }

    private void TryHeal()
    {
        if (!GetOwner().IsSpawned() || GetOwner().IsDead())
            return;
        if (!GetKnownList().StreamPlayers().Any(p => !p.IsDead() && !p.GetLifeStats().IsFullyRestoredHp() && IsInRange(p, 3)))
            return;
        GetOwner().SetTarget(GetOwner());
        SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 21837, 1, GetOwner()).UseSkill();
    }
}
